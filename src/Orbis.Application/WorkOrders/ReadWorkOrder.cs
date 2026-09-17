using Orbis.Domain.Tenancy;

namespace Orbis.Application.WorkOrders;

public sealed record TenantUser(Guid TenantId, Guid UserId);
public sealed record OrderDetails(Guid Id, string Description, string Status, long Version, DateTimeOffset CreatedAt);

public interface ITenantDirectory
{
    Task<TenantUser?> ResolveAsync(string host, string issuer, string subject, CancellationToken cancellationToken);
}

public interface IWorkOrderReader
{
    Task<OrderDetails?> FindAsync(TenantUser user, Guid orderId, CancellationToken cancellationToken);
}

public sealed class ReadWorkOrder(ITenantDirectory directory, IWorkOrderReader orders)
{
    public async Task<OrderDetails?> ExecuteAsync(string host, string issuer, string subject, Guid? claimedTenantId,
        Guid orderId, CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty || issuer.Length > 512 || subject.Length is < 1 or > 256) return null;
        string normalizedHost;
        try { normalizedHost = TenantDomain.NormalizeHost(host); }
        catch (ArgumentException) { return null; }
        var user = await directory.ResolveAsync(normalizedHost, issuer, subject, cancellationToken);
        // Uma claim de contexto pode restringir o token; nunca amplia os vínculos persistidos.
        if (user is null || (claimedTenantId.HasValue && claimedTenantId != user.TenantId)) return null;
        return await orders.FindAsync(user, orderId, cancellationToken);
    }
}
