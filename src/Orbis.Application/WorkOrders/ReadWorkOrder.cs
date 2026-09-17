using Orbis.Application.Tenancy;

namespace Orbis.Application.WorkOrders;

public sealed record OrderDetails(Guid Id, string Description, string Status, long Version, DateTimeOffset CreatedAt);

public interface IWorkOrderReader
{
    Task<OrderDetails?> FindAsync(TenantUser user, Guid orderId, CancellationToken cancellationToken);
    Task<OrderBatch?> ListAsync(TenantUser user, int limit, OrderPosition? position, CancellationToken cancellationToken);
}

public sealed class ReadWorkOrder(ResolveTenantUser resolver, IWorkOrderReader orders)
{
    public async Task<OrderDetails?> ExecuteAsync(TenantRequest request, Guid orderId, CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty) return null;
        var user = await resolver.ExecuteAsync(request, cancellationToken);
        if (user is null) return null;
        return await orders.FindAsync(user, orderId, cancellationToken);
    }
}
