namespace Orbis.Domain.WorkOrders;

public sealed class WorkOrderAudit
{
    public Guid TenantId { get; private set; }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    private WorkOrderAudit() { }

    // Auditoria registra autoria/efeito; a descrição do cliente não deve ser duplicada aqui.
    public static WorkOrderAudit Requested(WorkOrder order) => new()
    {
        TenantId = order.TenantId,
        Id = Guid.CreateVersion7(),
        ActorId = order.CustomerUserId,
        OrderId = order.Id,
        Action = "work-order.requested",
        OccurredAt = order.CreatedAt
    };
}
