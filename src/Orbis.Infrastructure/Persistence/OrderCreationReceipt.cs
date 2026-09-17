using Orbis.Domain.WorkOrders;

namespace Orbis.Infrastructure.Persistence;

public sealed class OrderCreationReceipt
{
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid Key { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OrderCreationReceipt() { }

    public OrderCreationReceipt(WorkOrder order, Guid key, string fingerprint)
    {
        if (key == Guid.Empty || fingerprint.Length != 64 || !fingerprint.All(char.IsAsciiHexDigit))
            throw new ArgumentException("A key and SHA-256 fingerprint are required.");
        TenantId = order.TenantId;
        ActorId = order.CustomerUserId;
        Key = key;
        Fingerprint = fingerprint;
        OrderId = order.Id;
        CreatedAt = order.CreatedAt;
    }
}
