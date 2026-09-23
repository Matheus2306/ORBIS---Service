using Orbis.Domain.Identity;

namespace Orbis.Application.Tenancy;

public sealed record CurrentContext(Guid TenantId, Guid UserId, string TenantName, Permission Permissions);
public sealed record CurrentUserDetails(Guid UserId, Guid TenantId);
public sealed record CurrentTenantDetails(Guid Id, string Name);
public sealed record CurrentPermissions(Guid TenantId, Guid UserId, IReadOnlyList<string> Permissions);

public interface ICurrentContextReader
{
    Task<CurrentContext?> FindAsync(TenantUser user, CancellationToken cancellationToken);
}

public sealed class ReadCurrentContext(ResolveTenantUser resolver, ICurrentContextReader contexts)
{
    public async Task<CurrentContext?> ExecuteAsync(TenantRequest request, CancellationToken cancellationToken)
    {
        var user = await resolver.ExecuteAsync(request, cancellationToken);
        // Identidade global sozinha não concede nem mesmo leitura dos metadados de outro tenant.
        return user is null ? null : await contexts.FindAsync(user, cancellationToken);
    }
}
