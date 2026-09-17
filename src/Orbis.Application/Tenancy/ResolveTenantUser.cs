using Orbis.Domain.Tenancy;

namespace Orbis.Application.Tenancy;

public sealed record TenantUser(Guid TenantId, Guid UserId);
public sealed record TenantRequest(string Host, string Issuer, string Subject, Guid? ClaimedTenantId);

public interface ITenantDirectory
{
    Task<TenantUser?> ResolveAsync(string host, string issuer, string subject, CancellationToken cancellationToken);
}

public sealed class ResolveTenantUser(ITenantDirectory directory)
{
    public async Task<TenantUser?> ExecuteAsync(TenantRequest request, CancellationToken cancellationToken)
    {
        if (request.Issuer.Length > 512 || request.Subject.Length is < 1 or > 256) return null;
        string normalizedHost;
        try { normalizedHost = TenantDomain.NormalizeHost(request.Host); }
        catch (ArgumentException) { return null; }
        var user = await directory.ResolveAsync(normalizedHost, request.Issuer, request.Subject, cancellationToken);
        // Uma claim de contexto pode restringir o token; nunca amplia os vínculos persistidos.
        return user is null || (request.ClaimedTenantId.HasValue && request.ClaimedTenantId != user.TenantId) ? null : user;
    }
}
