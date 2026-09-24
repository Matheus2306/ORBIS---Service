using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Administration.Api.Contracts;
using Orbis.Application.Identity;
using Orbis.Application.Memberships;
using Orbis.Domain.Identity;
using Orbis.Hosting;

namespace Orbis.Administration.Api.Controllers;

[ApiController]
[Route("v1/members/{id:guid}")]
[Authorize(Policy = AdministrativeAccessHandler.Policy)]
[EnableRateLimiting("administrative-operations")]
[RequestTimeout("administrative-command")]
[ProducesResponseType<MemberDetails>(StatusCodes.Status200OK)]
public sealed class MemberAccessController(ChangeMemberAccess change) : ControllerBase
{
    [HttpPatch("permissions")]
    public Task<IResult> Permissions(Guid id, [FromBody] MemberPermissionsBody body, CancellationToken cancellationToken) =>
        PermissionNames.TryParse(body.PermissionSet, out var permissions)
            ? ExecuteAsync(id, permissions, null, body.ExpectedVersion, cancellationToken)
            : Task.FromResult(Results.Problem(statusCode: 400, title: "Invalid permission set."));

    [HttpPost("suspend")]
    public Task<IResult> Suspend(Guid id, [FromBody] MemberStatusBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, null, false, body.ExpectedVersion, cancellationToken);

    [HttpPost("activate")]
    public Task<IResult> Activate(Guid id, [FromBody] MemberStatusBody body, CancellationToken cancellationToken) =>
        ExecuteAsync(id, null, true, body.ExpectedVersion, cancellationToken);

    private async Task<IResult> ExecuteAsync(Guid id, Permission? permissions, bool? isActive, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        if (!TenantHttpRequest.TryReadIdempotencyKey(HttpContext, out var key))
            return Results.Problem(statusCode: 400, title: "A nonempty UUID Idempotency-Key is required.");
        var result = await change.ExecuteAsync(request, new(id, permissions, isActive, expectedVersion, key), cancellationToken);
        if (result.Outcome == ChangeMemberAccessOutcome.Denied) return Results.NotFound();
        if (result.Outcome == ChangeMemberAccessOutcome.Invalid) return Results.Problem(statusCode: 400, title: "Invalid access change.");
        if (result.Outcome == ChangeMemberAccessOutcome.Conflict) return Results.Problem(statusCode: 409, title: "Access change conflicts with the current state or command key.");
        if (result.Outcome == ChangeMemberAccessOutcome.Busy)
        {
            Response.Headers.RetryAfter = "1";
            return Results.Problem(statusCode: 503, title: "Service temporarily unavailable.");
        }
        Response.Headers["Idempotency-Replayed"] = result.Outcome == ChangeMemberAccessOutcome.Replayed ? "true" : "false";
        var member = result.Member!;
        return Results.Ok(new MemberDetails(member.UserId, member.IsActive, PermissionNames.From(member.Permissions), member.Version));
    }
}
