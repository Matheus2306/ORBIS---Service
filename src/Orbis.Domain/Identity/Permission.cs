namespace Orbis.Domain.Identity;

[Flags]
public enum Permission
{
    None = 0,
    ReadOwnOrders = 1,
    ReadAllOrders = 2,
    CreateOrders = 4,
    AssignOrders = 8,
    ExecuteAssignedOrders = 16,
    CancelOwnOrders = 32,
    ManageOrders = 64
}
