using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orbis.Hosting;

public static class OrbisHttp
{
    public static void AddOrbisHttp(this WebApplicationBuilder builder, long bodyLimit = 32 * 1024)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<DependencyExceptionHandler>();
        builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            // Um único contrato de binding evita que um dos hosts passe a ecoar payloads ou erros do parser.
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
                    .CreateProblemDetails(context.HttpContext, StatusCodes.Status400BadRequest, title: "Invalid request."));
        });
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = bodyLimit);
    }
}
