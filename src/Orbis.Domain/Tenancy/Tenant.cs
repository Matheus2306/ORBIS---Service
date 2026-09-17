namespace Orbis.Domain.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Tenant() { }

    public Tenant(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Tenant identity is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 120) throw new ArgumentOutOfRangeException(nameof(name));
        Id = id;
        Name = name.Trim();
    }

    // Provisionamento incompleto permanece inacessível até ativação explícita do control plane.
    public void Activate() => IsActive = true;
    public void Suspend() => IsActive = false;
}
