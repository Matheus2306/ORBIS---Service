using Microsoft.EntityFrameworkCore;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;

namespace Orbis.Infrastructure.Queries;

public sealed class WorkOrderReader(DbContextOptions<TenantDbContext> options) : IWorkOrderReader
{
    public async Task<OrderDetails?> FindAsync(TenantUser user, Guid orderId, CancellationToken cancellationToken)
    {
        await using var database = new TenantDbContext(options, new TenantScope(user.TenantId));
        await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
        var member = await database.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);
        if (member is null || !member.IsActive) return null;
        var order = await database.WorkOrders.AsNoTracking().Where(WorkOrderAccess.ReadFilter(member))
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null) return null;
        return new OrderDetails(order.Id, order.Description, order.Status.ToString(), order.Version, order.CreatedAt);
    }

    public async Task<OrderBatch?> ListAsync(TenantUser user, int limit, OrderPosition? position, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var database = new TenantDbContext(options, new TenantScope(user.TenantId));
        await using var transaction = await database.BeginTenantTransactionAsync(cancellationToken);
        var member = await database.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);
        if (member is null || !(member.Allows(Permission.ReadAllOrders) || member.Allows(Permission.ReadOwnOrders) ||
            member.Allows(Permission.ExecuteAssignedOrders))) return null;
        var query = database.WorkOrders.AsNoTracking().Where(WorkOrderAccess.ReadFilter(member));
        if (position is not null)
            query = query.Where(order => EF.Functions.LessThan(ValueTuple.Create(order.CreatedAt, order.Id), ValueTuple.Create(position.CreatedAt, position.Id)));
        // Autorização e seek ocorrem no PostgreSQL antes do limite, sem offset ou contagem global.
        var rows = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id).Take(limit + 1)
            .Select(order => new { order.Id, order.Description, order.Status, order.Version, order.CreatedAt }).ToListAsync(cancellationToken);
        return new(rows.Take(limit).Select(order => new OrderDetails(order.Id, order.Description, order.Status.ToString(), order.Version, order.CreatedAt)).ToArray(), rows.Count > limit);
    }
}
