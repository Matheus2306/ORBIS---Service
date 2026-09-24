using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Orbis.Hosting;

public sealed class DependencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsDependencyFailure(exception)) return false;
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        // O cliente recebe uma falha temporária sem SQL, topologia ou mensagem da dependência.
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = 503, Title = "Service temporarily unavailable" }
        });
    }

    // EF encapsula falhas de transporte e integridade; somente as transitórias devem sugerir indisponibilidade.
    private static bool IsDependencyFailure(Exception exception) => exception switch
    {
        PostgresException postgres => postgres.IsTransient,
        NpgsqlException or TimeoutException => true,
        _ => exception.InnerException is not null && IsDependencyFailure(exception.InnerException)
    };
}
