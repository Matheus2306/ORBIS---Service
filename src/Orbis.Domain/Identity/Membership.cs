namespace Orbis.Domain.Identity;

public sealed class Membership
{
    private const Permission KnownPermissions = Permission.ReadOwnOrders | Permission.ReadAllOrders |
        Permission.CreateOrders | Permission.AssignOrders | Permission.ExecuteAssignedOrders |
        Permission.CancelOwnOrders | Permission.ManageOrders | Permission.ReadMembers;

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Permission Permissions { get; private set; }
    public bool IsActive { get; private set; }

    private Membership() { }

    public Membership(Guid tenantId, Guid userId, Permission permissions)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Tenant and user must have an identity.");
        if ((permissions & ~KnownPermissions) != 0)
            throw new ArgumentOutOfRangeException(nameof(permissions));

        TenantId = tenantId;
        UserId = userId;
        Permissions = permissions;
        IsActive = true;
    }

    public bool Allows(Permission permission) =>
        IsActive && permission != Permission.None && (Permissions & permission) == permission;

    // Suspensão revoga o vínculo inteiro, sem depender de claims antigas do token.
    public void Suspend() => IsActive = false;
}
