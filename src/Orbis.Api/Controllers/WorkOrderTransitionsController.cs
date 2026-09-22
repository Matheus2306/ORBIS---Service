using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Api.Contracts;
using Orbis.Application.WorkOrders;
using Orbis.Domain.WorkOrders;

namespace Orbis.Api.Controllers;

[ApiController]
[Route("v1/work-orders/{id:guid}")]
[Authorize(Policy = ApiPolicies.TenantAccess)]
[EnableRateLimiting("database-operations")]
[ProducesResponseType<TransitionedOrder>(StatusCodes.Status200OK)]
public sealed class WorkOrderTransitionsController(TransitionWorkOrder transition) : ControllerBase
{
    [HttpPost("assign")]
    public Task<IResult> Assign(Guid id, [FromBody] TransitionOrderBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, WorkOrderAction.Assign, body, cancellationToken);

    [HttpPost("accept")]
    public Task<IResult> Accept(Guid id, [FromBody] TransitionOrderBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, WorkOrderAction.Accept, body, cancellationToken);

    [HttpPost("start")]
    public Task<IResult> Start(Guid id, [FromBody] TransitionOrderBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, WorkOrderAction.Start, body, cancellationToken);

    [HttpPost("complete")]
    public Task<IResult> Complete(Guid id, [FromBody] TransitionOrderBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, WorkOrderAction.Complete, body, cancellationToken);

    [HttpPost("cancel")]
    public Task<IResult> Cancel(Guid id, [FromBody] TransitionOrderBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, WorkOrderAction.Cancel, body, cancellationToken);

    private async Task<IResult> ExecuteAsync(Guid id, WorkOrderAction action, TransitionOrderBody body, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        if (!TenantHttpRequest.TryReadIdempotencyKey(HttpContext, out var key))
            return Results.Problem(statusCode: 400, title: "A nonempty UUID Idempotency-Key is required.");
        // As cinco rotas compartilham a aplicação das regras, autorização de recurso e recibo transacional.
        var result = await transition.ExecuteAsync(request, new(id, action, body.ExpectedVersion, key, body.ProviderUserId), cancellationToken);
        if (result.Outcome == TransitionOrderOutcome.Denied) return Results.NotFound();
        if (result.Outcome == TransitionOrderOutcome.Invalid) return Results.Problem(statusCode: 400, title: "Invalid transition request.");
        if (result.Outcome == TransitionOrderOutcome.Conflict) return Results.Problem(statusCode: 409, title: "Order version, state or idempotency key conflicts with this request.");
        Response.Headers["Idempotency-Replayed"] = result.Outcome == TransitionOrderOutcome.Replayed ? "true" : "false";
        return Results.Ok(result.Order);
    }
}
