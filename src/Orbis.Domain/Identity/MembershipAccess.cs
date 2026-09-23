namespace Orbis.Domain.Identity;

public static class MembershipAccess
{
    public static bool CanChange(Membership actor, Membership target, Permission desired) =>
        actor.TenantId == target.TenantId && actor.Allows(Permission.ManageMembers) &&
        Membership.ArePermissionsValid(desired) &&
        // Suspender ou retirar flags de um administrador mais poderoso também seria escalada de privilégio.
        (target.Permissions & ~actor.Permissions) == 0 && (desired & ~actor.Permissions) == 0;
}
