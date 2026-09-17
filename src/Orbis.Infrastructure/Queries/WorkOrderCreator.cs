using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class WorkOrderCreator(DbContextOptions<TenantDbContext> options, TimeProvider clock) : IWorkOrderCreator
{
    public async Task<CreateOrderResult> CreateAsync(TenantUser user, Guid key, string description, string fingerprint, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var database = new TenantDbContext(options, new TenantScope(user.TenantId));
            await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
            var member = await database.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);
            if (member is null || !member.Allows(Permission.CreateOrders)) return new(CreateOrderOutcome.Denied);

            var receipt = await database.CreationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ActorId == user.UserId && x.Key == key, cancellationToken);
            if (receipt is not null)
                return receipt.Fingerprint == fingerprint
                    ? new(CreateOrderOutcome.Replayed, new(receipt.OrderId, receipt.CreatedAt))
                    : new(CreateOrderOutcome.Conflict);

            var now = clock.GetUtcNow();
            // PostgreSQL persiste microssegundos: a primeira resposta e o replay devem ser idênticos.
            now = now.AddTicks(-(now.Ticks % 10));
            var order = WorkOrder.Request(user.TenantId, user.UserId, description, now);
            database.AddRange(order, new OrderCreationReceipt(order, key, fingerprint), WorkOrderAudit.Requested(order));
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(CreateOrderOutcome.Created, new(order.Id, order.CreatedAt));
            }
            catch (DbUpdateException exception) when (attempt == 0 && exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_order_creation_receipts" })
            {
                // Somente a disputa idempotente admite releitura: rollback elimina ordem e audit perdedores.
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        throw new InvalidOperationException("An idempotent creation could not be resolved.");
    }
}
