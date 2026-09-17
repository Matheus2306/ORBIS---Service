using Orbis.Domain.WorkOrders;

namespace Orbis.Tests;

public sealed class WorkOrderTests
{
    private static WorkOrder NewOrder() => WorkOrder.Request(Guid.NewGuid(), Guid.NewGuid(), "Repair equipment", DateTimeOffset.UnixEpoch);

    [Fact]
    public void OnlyAssignedProviderCanCompleteTheFullLifecycle()
    {
        var order = NewOrder();
        var provider = Guid.NewGuid();
        order.Assign(provider);
        Assert.Throws<InvalidOperationException>(() => order.Accept(Guid.NewGuid()));
        order.Accept(provider);
        Assert.Throws<InvalidOperationException>(() => order.Complete(provider));
        order.Start(provider);
        order.Complete(provider);
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(5, order.Version);
        Assert.Throws<InvalidOperationException>(() => order.Complete(provider));
        Assert.Throws<InvalidOperationException>(order.Cancel);
        Assert.Equal(5, order.Version);
    }

    [Fact]
    public void CancellationCannotDiscardWorkAlreadyInProgress()
    {
        var order = NewOrder();
        var provider = Guid.NewGuid();
        order.Assign(provider);
        order.Accept(provider);
        order.Start(provider);
        Assert.Throws<InvalidOperationException>(order.Cancel);
        Assert.Equal(WorkOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void CancelledOrderCannotBeAssigned()
    {
        var order = NewOrder();
        order.Cancel();
        Assert.Throws<InvalidOperationException>(() => order.Assign(Guid.NewGuid()));
        Assert.Null(order.ProviderUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequestRejectsEmptyDescription(string description) =>
        Assert.Throws<ArgumentException>(() => WorkOrder.Request(Guid.NewGuid(), Guid.NewGuid(), description, DateTimeOffset.UnixEpoch));

    [Fact]
    public void RequestRejectsOversizedDescription() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkOrder.Request(Guid.NewGuid(), Guid.NewGuid(), new string('a', 2001), DateTimeOffset.UnixEpoch));

    [Fact]
    public void RequestRejectsMissingTenant() =>
        Assert.Throws<ArgumentException>(() => WorkOrder.Request(Guid.Empty, Guid.NewGuid(), "Repair", DateTimeOffset.UnixEpoch));
}
