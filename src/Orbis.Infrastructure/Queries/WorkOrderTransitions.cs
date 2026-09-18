using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class WorkOrderTransitions(DbContextOptions<TenantDbContext> options, TimeProvider clock) : IWorkOrderTransitions
{
    public async Task<TransitionOrderResult> ExecuteAsync(TenantUser user, TransitionOrderCommand command, string fingerprint, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var database = new TenantDbContext(options, new TenantScope(user.TenantId));
            await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
            var member = await database.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);
            if (member is null || !member.IsActive) return new(TransitionOrderOutcome.Denied);
            var order = await database.WorkOrders.SingleOrDefaultAsync(x => x.Id == command.OrderId, cancellationToken);
            if (order is null) return new(TransitionOrderOutcome.Denied);
            var allowed = command.Action switch
            {
                WorkOrderAction.Assign => false,
                WorkOrderAction.Cancel => WorkOrderAccess.CanCancel(member, order),
                WorkOrderAction.Accept or WorkOrderAction.Start or WorkOrderAction.Complete => WorkOrderAccess.CanExecute(member, order),
                _ => false
            };
            if (command.Action == WorkOrderAction.Assign && command.ProviderId.HasValue)
            {
                // Suspensão global também impede novas atribuições, mesmo com membership ainda ativo.
                var provider = await database.Memberships.FromSqlInterpolated($"""
                    SELECT m.* FROM memberships AS m JOIN directory.users AS u ON u.id=m.user_id
                    WHERE m.user_id={command.ProviderId.Value} AND u.is_active
                    """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
                allowed = provider is not null && WorkOrderAccess.CanAssign(member, order, provider);
            }
            if (!allowed) return new(TransitionOrderOutcome.Denied);

            var receipt = await database.TransitionReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ActorId == user.UserId && x.Action == command.Action && x.Key == command.Key, cancellationToken);
            if (receipt is not null)
                return receipt.Fingerprint == fingerprint
                    ? new(TransitionOrderOutcome.Replayed, new(receipt.OrderId, receipt.Status.ToString(), receipt.Version))
                    : new(TransitionOrderOutcome.Conflict);
            if (order.Version != command.ExpectedVersion) return new(TransitionOrderOutcome.Conflict);
            try
            {
                switch (command.Action)
                {
                    case WorkOrderAction.Assign: order.Assign(command.ProviderId!.Value); break;
                    case WorkOrderAction.Accept: order.Accept(user.UserId); break;
                    case WorkOrderAction.Start: order.Start(user.UserId); break;
                    case WorkOrderAction.Complete: order.Complete(user.UserId); break;
                    case WorkOrderAction.Cancel: order.Cancel(); break;
                    default: return new(TransitionOrderOutcome.Invalid);
                }
            }
            catch (InvalidOperationException) { return new(TransitionOrderOutcome.Conflict); }
            var now = clock.GetUtcNow();
            database.AddRange(new OrderTransitionReceipt(order, user.UserId, command.Action, command.Key, fingerprint, now),
                WorkOrderAudit.Transitioned(order, user.UserId, command.Action, now));
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(TransitionOrderOutcome.Applied, new(order.Id, order.Status.ToString(), order.Version));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == 1) return new(TransitionOrderOutcome.Conflict);
                // Releitura distingue um replay vencedor de uma versão consumida por outro comando.
            }
            catch (DbUpdateException exception) when (attempt == 0 && exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_order_transition_receipts" })
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        throw new InvalidOperationException("An idempotent transition could not be resolved.");
    }
}
