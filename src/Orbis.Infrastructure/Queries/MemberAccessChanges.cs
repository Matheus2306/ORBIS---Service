using System.Data;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

// Registro somente no futuro host administrativo; a API comum não possui os grants usados aqui.
public sealed class MemberAccessChanges(DbContextOptions<TenantDbContext> options, TimeProvider clock) : IMemberAccessChanges
{
    private static readonly Meter Meter = new("Orbis.MembershipAdministration");
    private static readonly Counter<long> Retries = Meter.CreateCounter<long>("orbis.membership.transaction_retries");
    private static readonly Counter<long> Results = Meter.CreateCounter<long>("orbis.membership.access_changes");

    public async Task<ChangeMemberAccessResult> ExecuteAsync(TenantUser actor, ChangeMemberAccessCommand command, CancellationToken cancellationToken)
    {
        if (!command.IsValid) return Count(new(ChangeMemberAccessOutcome.Invalid));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try { return Count(await ExecuteOnceAsync(actor, command, cancellationToken)); }
            catch (Exception exception) when (CanRetryTransaction(exception))
            {
                // Repetir a transação inteira renova autorização e snapshot; nunca repetir só o UPDATE.
                if (attempt < 2)
                {
                    Retries.Add(1);
                    await Task.Delay(Random.Shared.Next(5, 26) * (attempt + 1), cancellationToken);
                }
            }
        }
        return Count(new(ChangeMemberAccessOutcome.Busy));
    }

    private async Task<ChangeMemberAccessResult> ExecuteOnceAsync(TenantUser actor, ChangeMemberAccessCommand command, CancellationToken cancellationToken)
    {
        await using var database = new TenantDbContext(options, new TenantScope(actor.TenantId));
        await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken, IsolationLevel.Serializable);
        // O resolver precede a transação; revalidar estado global evita autorizar a partir de um resultado já revogado.
        var active = await database.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM directory.tenants t, directory.users u
                WHERE t.id={actor.TenantId} AND t.is_active AND u.id={actor.UserId} AND u.is_active) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!active) return new(ChangeMemberAccessOutcome.Denied);
        var member = await database.Memberships.SingleOrDefaultAsync(x => x.UserId == actor.UserId, cancellationToken);
        if (member is null || !member.Allows(Permission.ManageMembers)) return new(ChangeMemberAccessOutcome.Denied);
        var target = await database.Memberships.SingleOrDefaultAsync(x => x.UserId == command.MemberId, cancellationToken);
        if (target is null || !MembershipAccess.CanChange(member, target, command.Permissions)) return new(ChangeMemberAccessOutcome.Denied);

        var fingerprint = command.Fingerprint();
        var receipt = await database.MembershipAccessChanges.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorId == actor.UserId && x.Key == command.Key, cancellationToken);
        if (receipt is not null)
            return receipt.Fingerprint == fingerprint
                ? new(ChangeMemberAccessOutcome.Replayed, new(receipt.MemberId, receipt.Permissions, receipt.IsActive, receipt.Version))
                : new(ChangeMemberAccessOutcome.Conflict);
        if (target.Version != command.ExpectedVersion || (target.Permissions == command.Permissions && target.IsActive == command.IsActive))
            return new(ChangeMemberAccessOutcome.Conflict);
        if (command.IsActive && !await database.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM directory.users WHERE id={target.UserId} AND is_active) AS "Value"
            """).SingleAsync(cancellationToken)) return new(ChangeMemberAccessOutcome.Denied);

        if (target.Allows(Permission.ManageMembers) && (!command.IsActive || (command.Permissions & Permission.ManageMembers) == 0))
        {
            // O predicado participa da SSI: dois administradores não podem remover simultaneamente o último acesso.
            var another = await database.Memberships.FromSqlInterpolated($"""
                SELECT m.* FROM public.memberships m JOIN directory.users u ON u.id=m.user_id
                WHERE m.user_id<>{target.UserId} AND m.is_active AND (m.permissions & 256)=256 AND u.is_active
                """).AnyAsync(cancellationToken);
            if (!another) return new(ChangeMemberAccessOutcome.Conflict);
        }
        var change = MembershipAccessChange.Apply(member, target, command.Permissions, command.IsActive, command.Key, fingerprint, clock.GetUtcNow());
        database.MembershipAccessChanges.Add(change);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ChangeMemberAccessOutcome.Applied, new(target.UserId, target.Permissions, target.IsActive, target.Version));
    }

    private static bool CanRetryTransaction(Exception exception) => exception is PostgresException
    { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } or
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_membership_access_changes" } ||
        exception.InnerException is not null && CanRetryTransaction(exception.InnerException);

    private static ChangeMemberAccessResult Count(ChangeMemberAccessResult result)
    {
        // Sem IDs de usuário/tenant: cardinalidade limitada e nenhum dado pessoal em métricas.
        Results.Add(1, new KeyValuePair<string, object?>("outcome", result.Outcome.ToString()));
        return result;
    }
}
