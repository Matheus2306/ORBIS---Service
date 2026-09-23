using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Application.Identity;
using Orbis.Application.Tenancy;

namespace Orbis.Api.Controllers;

[ApiController]
[Route("v1/me")]
[Authorize(Policy = ApiPolicies.TenantAccess)]
[EnableRateLimiting("database-operations")]
public sealed class CurrentUserController(ReadCurrentContext read) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CurrentUserDetails>(StatusCodes.Status200OK)]
    public async Task<IResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        var context = await read.ExecuteAsync(request, cancellationToken);
        return context is null ? Results.NotFound() : Results.Ok(new CurrentUserDetails(context.UserId, context.TenantId));
    }

    [HttpGet("permissions")]
    [ProducesResponseType<CurrentPermissions>(StatusCodes.Status200OK)]
    public async Task<IResult> Permissions(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        var context = await read.ExecuteAsync(request, cancellationToken);
        if (context is null) return Results.NotFound();
        // A resposta orienta a UI; comandos continuam revalidando as permissões persistidas em cada acesso.
        return Results.Ok(new CurrentPermissions(context.TenantId, context.UserId, PermissionNames.From(context.Permissions)));
    }
}
