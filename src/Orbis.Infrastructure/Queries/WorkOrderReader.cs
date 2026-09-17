using Microsoft.EntityFrameworkCore;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
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
        var order = await database.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null || !WorkOrderAccess.CanRead(member, order)) return null;
        return new OrderDetails(order.Id, order.Description, order.Status.ToString(), order.Version, order.CreatedAt);
    }
}
