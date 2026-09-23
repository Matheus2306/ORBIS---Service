namespace Orbis.Domain.Identity;

public sealed class Membership
{
    private const Permission KnownPermissions = Permission.ReadOwnOrders | Permission.ReadAllOrders |
        Permission.CreateOrders | Permission.AssignOrders | Permission.ExecuteAssignedOrders |
        Permission.CancelOwnOrders | Permission.ManageOrders | Permission.ReadMembers | Permission.ManageMembers;

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Permission Permissions { get; private set; }
    public bool IsActive { get; private set; }
    public long Version { get; private set; } = 1;

    private Membership() { }

    public Membership(Guid tenantId, Guid userId, Permission permissions)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Tenant and user must have an identity.");
        if (!ArePermissionsValid(permissions))
            throw new ArgumentOutOfRangeException(nameof(permissions));

        TenantId = tenantId;
        UserId = userId;
        Permissions = permissions;
        IsActive = true;
    }

    public bool Allows(Permission permission) =>
        IsActive && permission != Permission.None && (Permissions & permission) == permission;

    // Suspensão revoga o vínculo inteiro, sem depender de claims antigas do token.
    public void Suspend() => ChangeAccess(Permissions, false);

    public static bool ArePermissionsValid(Permission permissions) => (permissions & ~KnownPermissions) == 0;

    public bool ChangeAccess(Permission permissions, bool isActive)
    {
        if (!ArePermissionsValid(permissions)) throw new ArgumentOutOfRangeException(nameof(permissions));
        if (Permissions == permissions && IsActive == isActive) return false;
        // A versão muda também na suspensão; uma edição antiga não pode reativar um vínculo silenciosamente.
        var nextVersion = checked(Version + 1);
        Permissions = permissions;
        IsActive = isActive;
        Version = nextVersion;
        return true;
    }
}
