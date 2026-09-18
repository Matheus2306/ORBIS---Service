using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orbis.Application.Tenancy;
using Orbis.Domain.WorkOrders;

namespace Orbis.Application.WorkOrders;

public enum TransitionOrderOutcome { Applied, Replayed, Denied, Invalid, Conflict }
public sealed record TransitionedOrder(Guid Id, string Status, long Version);
public sealed record TransitionOrderResult(TransitionOrderOutcome Outcome, TransitionedOrder? Order = null);
public sealed record TransitionOrderCommand(Guid OrderId, WorkOrderAction Action, long ExpectedVersion, Guid Key, Guid? ProviderId);

public interface IWorkOrderTransitions
{
    Task<TransitionOrderResult> ExecuteAsync(TenantUser user, TransitionOrderCommand command, string fingerprint, CancellationToken cancellationToken);
}

public sealed class TransitionWorkOrder(ResolveTenantUser resolver, IWorkOrderTransitions orders)
{
    public async Task<TransitionOrderResult> ExecuteAsync(TenantRequest request, TransitionOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.OrderId == Guid.Empty || command.Key == Guid.Empty || command.ExpectedVersion <= 0 || !Enum.IsDefined(command.Action) ||
            (command.Action == WorkOrderAction.Assign ? command.ProviderId is null || command.ProviderId == Guid.Empty : command.ProviderId is not null))
            return new(TransitionOrderOutcome.Invalid);
        var user = await resolver.ExecuteAsync(request, cancellationToken);
        if (user is null) return new(TransitionOrderOutcome.Denied);
        var canonical = string.Join('\n', "work-order.transition.v1", command.Action.ToString(), command.OrderId.ToString("D"),
            command.ExpectedVersion.ToString(CultureInfo.InvariantCulture), command.ProviderId?.ToString("D") ?? string.Empty);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return await orders.ExecuteAsync(user, command, fingerprint, cancellationToken);
    }
}
