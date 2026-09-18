namespace Orbis.Domain.WorkOrders;

public sealed class WorkOrderAudit
{
    public Guid TenantId { get; private set; }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public long OrderVersion { get; private set; }
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
        OrderVersion = order.Version,
        OccurredAt = order.CreatedAt
    };

    public static WorkOrderAudit Transitioned(WorkOrder order, Guid actorId, WorkOrderAction action, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || order.Version <= 1) throw new ArgumentException("Transition audit requires an actor and resulting version.");
        var name = action switch
        {
            WorkOrderAction.Assign when order.Status == WorkOrderStatus.Assigned => "work-order.assigned",
            WorkOrderAction.Accept when order.Status == WorkOrderStatus.Accepted => "work-order.accepted",
            WorkOrderAction.Start when order.Status == WorkOrderStatus.InProgress => "work-order.started",
            WorkOrderAction.Complete when order.Status == WorkOrderStatus.Completed => "work-order.completed",
            WorkOrderAction.Cancel when order.Status == WorkOrderStatus.Cancelled => "work-order.cancelled",
            _ => throw new ArgumentException("Audit action must match the resulting state.", nameof(action))
        };
        return new()
        {
            TenantId = order.TenantId,
            Id = Guid.CreateVersion7(),
            ActorId = actorId,
            OrderId = order.Id,
            Action = name,
            OrderVersion = order.Version,
            OccurredAt = now.ToUniversalTime()
        };
    }
}
