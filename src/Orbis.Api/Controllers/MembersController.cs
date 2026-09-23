using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Application.Memberships;

namespace Orbis.Api.Controllers;

[ApiController]
[Route("v1/members")]
[Authorize(Policy = ApiPolicies.TenantAccess)]
[EnableRateLimiting("database-operations")]
public sealed class MembersController(ReadMember read, ListMembers list) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MemberDetails>(StatusCodes.Status200OK)]
    public async Task<IResult> Get(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        var member = await read.ExecuteAsync(request, id, cancellationToken);
        return member is null ? Results.NotFound() : Results.Ok(member);
    }

    [HttpGet]
    [ProducesResponseType<MemberPage>(StatusCodes.Status200OK)]
    public async Task<IResult> List([FromQuery] int? limit, [FromQuery] string? cursor, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        if (Request.Query["limit"].Count > 1 || Request.Query["cursor"].Count > 1 || Request.Query["status"].Count > 1 ||
            (Request.Query.ContainsKey("limit") && limit is null))
            return Results.Problem(statusCode: 400, title: "Invalid member page parameters.");
        // Parâmetros fornecidos vazios não podem virar ausência/default por normalização do MVC.
        if (cursor is null && Request.Query.ContainsKey("cursor")) cursor = string.Empty;
        if (status is null && Request.Query.ContainsKey("status")) status = string.Empty;
        var result = await list.ExecuteAsync(request, limit ?? 25, cursor, status, cancellationToken);
        if (result.Outcome == ListMembersOutcome.Denied) return Results.NotFound();
        if (result.Outcome == ListMembersOutcome.Invalid) return Results.Problem(statusCode: 400, title: "Invalid member page parameters.");
        return Results.Ok(result.Page);
    }
}
