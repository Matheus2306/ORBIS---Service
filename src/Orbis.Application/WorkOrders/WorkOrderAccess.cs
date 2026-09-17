using System.Linq.Expressions;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;

namespace Orbis.Application.WorkOrders;

public static class WorkOrderAccess
{
    public static Expression<Func<WorkOrder, bool>> ReadFilter(Membership membership)
    {
        var all = membership.Allows(Permission.ReadAllOrders);
        var own = membership.Allows(Permission.ReadOwnOrders);
        var assigned = membership.Allows(Permission.ExecuteAssignedOrders);
        return order => order.TenantId == membership.TenantId &&
            (all || (own && order.CustomerUserId == membership.UserId) || (assigned && order.ProviderUserId == membership.UserId));
    }

    public static bool CanAssign(Membership membership, WorkOrder order, Membership provider) =>
        SameTenant(membership, order) && membership.Allows(Permission.AssignOrders) &&
        SameTenant(provider, order) && provider.Allows(Permission.ExecuteAssignedOrders);

    public static bool CanExecute(Membership membership, WorkOrder order) =>
        SameTenant(membership, order) && membership.Allows(Permission.ExecuteAssignedOrders) &&
        order.ProviderUserId == membership.UserId;

    public static bool CanCancel(Membership membership, WorkOrder order) =>
        SameTenant(membership, order) &&
        (membership.Allows(Permission.ManageOrders) ||
         (membership.Allows(Permission.CancelOwnOrders) && order.CustomerUserId == membership.UserId));

    // Permissão ampla dentro da empresa nunca concede alcance a outra organização.
    private static bool SameTenant(Membership membership, WorkOrder order) =>
        membership.IsActive && membership.TenantId == order.TenantId;
}
