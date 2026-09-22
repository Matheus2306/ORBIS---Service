using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Api.Contracts;
using Orbis.Application.WorkOrders;

namespace Orbis.Api.Controllers;

[ApiController]
[Route("v1/work-orders")]
[Authorize(Policy = ApiPolicies.TenantAccess)]
[EnableRateLimiting("database-operations")]
public sealed class WorkOrdersController(ReadWorkOrder read, ListWorkOrders list, CreateWorkOrder create) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDetails>(StatusCodes.Status200OK)]
    public async Task<IResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        var order = await read.ExecuteAsync(request, id, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    [HttpGet]
    [ProducesResponseType<OrderPage>(StatusCodes.Status200OK)]
    public async Task<IResult> List([FromQuery] int? limit, [FromQuery] string? cursor, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        // MVC converte string vazia em null; cursor fornecido e inválido não pode reiniciar a paginação.
        if (cursor is null && Request.Query.ContainsKey("cursor")) cursor = string.Empty;
        if (Request.Query["limit"].Count > 1 || Request.Query["cursor"].Count > 1)
            return Results.Problem(statusCode: 400, title: "Invalid page limit or cursor.");
        var result = await list.ExecuteAsync(request, limit ?? 25, cursor, cancellationToken);
        if (result.Outcome == ListOrdersOutcome.Denied) return Results.NotFound();
        if (result.Outcome == ListOrdersOutcome.Invalid) return Results.Problem(statusCode: 400, title: "Invalid page limit or cursor.");
        return Results.Ok(result.Page);
    }

    [HttpPost]
    [ProducesResponseType<CreatedOrder>(StatusCodes.Status201Created)]
    public async Task<IResult> Create([FromBody] RequestOrderBody body, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        if (!TenantHttpRequest.TryReadIdempotencyKey(HttpContext, out var key))
            return Results.Problem(statusCode: 400, title: "A nonempty UUID Idempotency-Key is required.");
        var result = await create.ExecuteAsync(request, key, body.Description, cancellationToken);
        if (result.Outcome == CreateOrderOutcome.Denied) return Results.NotFound();
        if (result.Outcome == CreateOrderOutcome.Invalid) return Results.Problem(statusCode: 400, title: "Description must contain 1 to 2000 characters.");
        if (result.Outcome == CreateOrderOutcome.Conflict) return Results.Problem(statusCode: 409, title: "Idempotency key was already used with different content.");
        Response.Headers["Idempotency-Replayed"] = result.Outcome == CreateOrderOutcome.Replayed ? "true" : "false";
        // Location relativa preserva o contrato e não promove o Host recebido a URL confiável.
        return Results.Created($"/v1/work-orders/{result.Order!.Id}", result.Order);
    }
}
