namespace Orbis.Application.Tenancy;

public sealed class TenantScope
{
    public Guid TenantId { get; }

    public TenantScope(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant context is required.", nameof(tenantId));
        TenantId = tenantId;
    }
}
