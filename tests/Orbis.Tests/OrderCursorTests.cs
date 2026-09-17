using Orbis.Application.WorkOrders;

namespace Orbis.Tests;

public sealed class OrderCursorTests
{
    [Fact]
    public void CursorRejectsUnboundedOrOutOfRangePositions()
    {
        var valid = new OrderCursor(1, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch.UtcTicks, Guid.NewGuid());
        Assert.Equal(valid, OrderCursor.Decode(valid.Encode()));
        Assert.Null(OrderCursor.Decode(new string('a', 513)));
        Assert.Null(OrderCursor.Decode((valid with { Version = 2 }).Encode()));
        Assert.Null(OrderCursor.Decode((valid with { UtcTicks = -1 }).Encode()));
        Assert.Null(OrderCursor.Decode((valid with { UtcTicks = long.MaxValue }).Encode()));
        Assert.Null(OrderCursor.Decode((valid with { TenantId = Guid.Empty }).Encode()));
    }
}
