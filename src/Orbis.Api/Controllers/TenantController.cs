using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Application.Tenancy;

namespace Orbis.Api.Controllers;

[ApiController]
[Route("v1/tenant")]
[Authorize(Policy = ApiPolicies.TenantAccess)]
[EnableRateLimiting("database-operations")]
public sealed class TenantController(ReadCurrentContext read) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CurrentTenantDetails>(StatusCodes.Status200OK)]
    public async Task<IResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TenantHttpRequest.TryRead(HttpContext, out var request)) return Results.NotFound();
        var context = await read.ExecuteAsync(request, cancellationToken);
        return context is null ? Results.NotFound() : Results.Ok(new CurrentTenantDetails(context.TenantId, context.TenantName));
    }
}
