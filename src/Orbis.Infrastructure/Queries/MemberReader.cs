using Microsoft.EntityFrameworkCore;
using Orbis.Application.Identity;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class MemberReader(DbContextOptions<TenantDbContext> options) : IMemberReader
{
    public async Task<MemberDetails?> FindAsync(TenantUser actor, Guid userId, CancellationToken cancellationToken)
    {
        await using var database = new TenantDbContext(options, new TenantScope(actor.TenantId));
        await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
        if (!await CanReadAsync(database, actor.UserId, cancellationToken)) return null;
        var member = await database.Memberships.AsNoTracking().Where(member => member.UserId == userId)
            .Select(member => new { member.UserId, member.IsActive, member.Permissions, member.Version }).SingleOrDefaultAsync(cancellationToken);
        return member is null ? null : new(member.UserId, member.IsActive, PermissionNames.From(member.Permissions), member.Version);
    }

    public async Task<MemberBatch?> ListAsync(TenantUser actor, int limit, Guid? afterUserId, MemberStatusFilter status, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100 || !Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var database = new TenantDbContext(options, new TenantScope(actor.TenantId));
        await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
        if (!await CanReadAsync(database, actor.UserId, cancellationToken)) return null;
        var query = database.Memberships.AsNoTracking();
        if (status != MemberStatusFilter.All) query = query.Where(member => member.IsActive == (status == MemberStatusFilter.Active));
        if (afterUserId.HasValue)
            query = query.Where(member => EF.Functions.GreaterThan(ValueTuple.Create(member.UserId), ValueTuple.Create(afterUserId.Value)));
        // A PK (tenant_id,user_id) fornece ordem estável; buscar só limit+1, sem COUNT/offset ou dados do diretório global.
        var rows = await query.OrderBy(member => member.UserId).Take(limit + 1)
            .Select(member => new { member.UserId, member.IsActive, member.Permissions, member.Version }).ToListAsync(cancellationToken);
        return new(rows.Take(limit).Select(member => new MemberDetails(member.UserId, member.IsActive, PermissionNames.From(member.Permissions), member.Version)).ToArray(),
            rows.Count > limit);
    }

    private static Task<bool> CanReadAsync(TenantDbContext database, Guid actorId, CancellationToken cancellationToken) =>
        database.Memberships.AnyAsync(member => member.UserId == actorId && member.IsActive &&
            (member.Permissions & Permission.ReadMembers) == Permission.ReadMembers, cancellationToken);
}
