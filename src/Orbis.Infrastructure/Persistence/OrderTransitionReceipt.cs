using Orbis.Domain.WorkOrders;

namespace Orbis.Infrastructure.Persistence;

public sealed class OrderTransitionReceipt
{
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public WorkOrderAction Action { get; private set; }
    public Guid Key { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OrderTransitionReceipt() { }

    public OrderTransitionReceipt(WorkOrder order, Guid actorId, WorkOrderAction action, Guid key, string fingerprint, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || key == Guid.Empty || !Enum.IsDefined(action) || fingerprint.Length != 64 || !fingerprint.All(char.IsAsciiHexDigit))
            throw new ArgumentException("A valid actor, action, key and fingerprint are required.");
        TenantId = order.TenantId;
        ActorId = actorId;
        Action = action;
        Key = key;
        Fingerprint = fingerprint;
        OrderId = order.Id;
        Status = order.Status;
        Version = order.Version;
        CreatedAt = now.ToUniversalTime();
    }
}
