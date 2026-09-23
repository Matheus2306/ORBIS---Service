namespace Orbis.Domain.Identity;

// O mesmo registro imutável é evidência de auditoria e recibo; sua retenção deve atender aos dois contratos.
public sealed class MembershipAccessChange
{
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid Key { get; private set; }
    public Guid MemberId { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public Permission PreviousPermissions { get; private set; }
    public bool PreviousIsActive { get; private set; }
    public long PreviousVersion { get; private set; }
    public Permission Permissions { get; private set; }
    public bool IsActive { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private MembershipAccessChange() { }

    public static MembershipAccessChange Apply(Membership actor, Membership target, Permission desired, bool isActive,
        Guid key, string fingerprint, DateTimeOffset now)
    {
        if (!MembershipAccess.CanChange(actor, target, desired)) throw new InvalidOperationException("Access change is forbidden.");
        if (key == Guid.Empty || fingerprint.Length != 64 || fingerprint.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
            throw new ArgumentException("A command identity and fingerprint are required.");
        var change = new MembershipAccessChange
        {
            TenantId = target.TenantId,
            ActorId = actor.UserId,
            Key = key,
            MemberId = target.UserId,
            Fingerprint = fingerprint,
            PreviousPermissions = target.Permissions,
            PreviousIsActive = target.IsActive,
            PreviousVersion = target.Version,
            Permissions = desired,
            IsActive = isActive,
            OccurredAt = now
        };
        if (!target.ChangeAccess(desired, isActive)) throw new InvalidOperationException("Access is unchanged.");
        change.Version = target.Version;
        return change;
    }
}
