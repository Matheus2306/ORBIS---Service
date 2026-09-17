using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Orbis.Api;

public sealed class DependencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (NpgsqlException or TimeoutException)) return false;
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        // O cliente recebe uma falha temporária sem SQL, topologia ou mensagem da dependência.
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = 503, Title = "Service temporarily unavailable" }
        });
    }
}
