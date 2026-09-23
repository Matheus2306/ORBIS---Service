using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Domain.Identity;
using Orbis.Domain.WorkOrders;

namespace Orbis.Tests;

public sealed class IsolationTests
{
    [Fact]
    public void AdministratorInACannotReadCancelOrAssignOrderInB()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var user = Guid.NewGuid();
        var member = new Membership(tenantA, user, Permission.ReadAllOrders | Permission.ManageOrders | Permission.AssignOrders);
        var provider = new Membership(tenantA, Guid.NewGuid(), Permission.ExecuteAssignedOrders);
        var order = WorkOrder.Request(tenantB, user, "Tenant B confidential", DateTimeOffset.UnixEpoch);
        Assert.False(WorkOrderAccess.ReadFilter(member).Compile()(order));
        Assert.False(WorkOrderAccess.CanCancel(member, order));
        Assert.False(WorkOrderAccess.CanAssign(member, order, provider));
    }

    [Fact]
    public void CustomerCannotReadAnotherCustomersOrderWithinSameTenant()
    {
        var tenant = Guid.NewGuid();
        var member = new Membership(tenant, Guid.NewGuid(), Permission.ReadOwnOrders | Permission.CancelOwnOrders);
        var own = WorkOrder.Request(tenant, member.UserId, "Own", DateTimeOffset.UnixEpoch);
        var other = WorkOrder.Request(tenant, Guid.NewGuid(), "Other", DateTimeOffset.UnixEpoch);
        Assert.True(WorkOrderAccess.ReadFilter(member).Compile()(own));
        Assert.False(WorkOrderAccess.ReadFilter(member).Compile()(other));
        Assert.False(WorkOrderAccess.CanCancel(member, other));
        member.Suspend();
        Assert.False(WorkOrderAccess.ReadFilter(member).Compile()(own));
    }

    [Fact]
    public void AssignmentRequiresActiveProviderInSameTenant()
    {
        var tenant = Guid.NewGuid();
        var admin = new Membership(tenant, Guid.NewGuid(), Permission.AssignOrders);
        var provider = new Membership(tenant, Guid.NewGuid(), Permission.ExecuteAssignedOrders);
        var foreignProvider = new Membership(Guid.NewGuid(), provider.UserId, Permission.ExecuteAssignedOrders);
        var order = WorkOrder.Request(tenant, admin.UserId, "Repair", DateTimeOffset.UnixEpoch);
        Assert.True(WorkOrderAccess.CanAssign(admin, order, provider));
        Assert.False(WorkOrderAccess.CanAssign(admin, order, foreignProvider));
        order.Assign(provider.UserId);
        Assert.True(WorkOrderAccess.CanExecute(provider, order));
        Assert.False(WorkOrderAccess.CanExecute(foreignProvider, order));
        provider.Suspend();
        Assert.False(WorkOrderAccess.CanExecute(provider, order));
    }

    [Fact]
    public void MissingContextAndUnknownPermissionsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => new TenantScope(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Membership(Guid.NewGuid(), Guid.NewGuid(), (Permission)256));
        Assert.False(new Membership(Guid.NewGuid(), Guid.NewGuid(), Permission.None).Allows(Permission.None));
    }
}
