using System.Security.Cryptography;
using System.Text;
using Orbis.Application.Tenancy;

namespace Orbis.Application.WorkOrders;

public enum CreateOrderOutcome { Created, Replayed, Denied, Invalid, Conflict }
public sealed record CreatedOrder(Guid Id, DateTimeOffset CreatedAt);
public sealed record CreateOrderResult(CreateOrderOutcome Outcome, CreatedOrder? Order = null);

public interface IWorkOrderCreator
{
    Task<CreateOrderResult> CreateAsync(TenantUser user, Guid key, string description, string fingerprint, CancellationToken cancellationToken);
}

public sealed class CreateWorkOrder(ResolveTenantUser resolver, IWorkOrderCreator orders)
{
    public async Task<CreateOrderResult> ExecuteAsync(TenantRequest request, Guid key, string? description, CancellationToken cancellationToken)
    {
        if (key == Guid.Empty || string.IsNullOrWhiteSpace(description) || description.Length > 2000 || description.Contains('\0'))
            return new(CreateOrderOutcome.Invalid);
        var user = await resolver.ExecuteAsync(request, cancellationToken);
        if (user is null) return new(CreateOrderOutcome.Denied);
        var normalized = description.Trim();
        // O fingerprint versionado representa o efeito sem persistir payload pessoal no recibo.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("work-order.create.v1\n" + normalized)));
        return await orders.CreateAsync(user, key, normalized, fingerprint, cancellationToken);
    }
}
