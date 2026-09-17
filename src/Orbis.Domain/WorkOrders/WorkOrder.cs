namespace Orbis.Domain.WorkOrders;

public sealed class WorkOrder
{
    public Guid TenantId { get; private set; }
    public Guid Id { get; private set; }
    public Guid CustomerUserId { get; private set; }
    public Guid? ProviderUserId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public WorkOrderStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private WorkOrder() { }

    public static WorkOrder Request(Guid tenantId, Guid customerUserId, string description, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || customerUserId == Guid.Empty)
            throw new ArgumentException("Tenant and customer must have an identity.");
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 2000)
            throw new ArgumentOutOfRangeException(nameof(description));
        if (description.Contains('\0'))
            throw new ArgumentException("Description contains an unsupported control character.", nameof(description));

        return new WorkOrder
        {
            TenantId = tenantId,
            Id = Guid.CreateVersion7(),
            CustomerUserId = customerUserId,
            Description = description.Trim(),
            Status = WorkOrderStatus.Requested,
            Version = 1,
            CreatedAt = now.ToUniversalTime()
        };
    }

    public void Assign(Guid providerUserId)
    {
        if (providerUserId == Guid.Empty)
            throw new ArgumentException("Provider must have an identity.", nameof(providerUserId));
        RequireStatus(WorkOrderStatus.Requested);
        ProviderUserId = providerUserId;
        MoveTo(WorkOrderStatus.Assigned);
    }

    public void Accept(Guid providerUserId)
    {
        RequireProvider(providerUserId);
        RequireStatus(WorkOrderStatus.Assigned);
        MoveTo(WorkOrderStatus.Accepted);
    }

    public void Start(Guid providerUserId)
    {
        RequireProvider(providerUserId);
        RequireStatus(WorkOrderStatus.Accepted);
        MoveTo(WorkOrderStatus.InProgress);
    }

    public void Complete(Guid providerUserId)
    {
        RequireProvider(providerUserId);
        RequireStatus(WorkOrderStatus.InProgress);
        MoveTo(WorkOrderStatus.Completed);
    }

    public void Cancel()
    {
        // Trabalho iniciado exige um fluxo de interrupção próprio, ainda fora deste agregado.
        if (Status is not (WorkOrderStatus.Requested or WorkOrderStatus.Assigned or WorkOrderStatus.Accepted))
            throw new InvalidOperationException("This order can no longer be cancelled.");
        MoveTo(WorkOrderStatus.Cancelled);
    }

    private void RequireProvider(Guid providerUserId)
    {
        if (providerUserId == Guid.Empty || ProviderUserId != providerUserId)
            throw new InvalidOperationException("Only the assigned provider can execute this order.");
    }

    private void RequireStatus(WorkOrderStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException("Invalid work order transition.");
    }

    private void MoveTo(WorkOrderStatus status)
    {
        // A versão muda com cada efeito de negócio para impedir atualização perdida no banco.
        Version = checked(Version + 1);
        Status = status;
    }
}
