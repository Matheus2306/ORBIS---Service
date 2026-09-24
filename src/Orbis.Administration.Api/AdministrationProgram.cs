using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Hosting;
using Orbis.Infrastructure.Queries;

namespace Orbis.Administration.Api;

public partial class AdministrationProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddOrbisHttp(8 * 1024);
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.AllowDuplicateProperties = false;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        });
        builder.Services.AddOpenApi();
        builder.Services.AddOrbisJwt("Administration:Authentication");
        builder.Services.AddOptions<AdministrationSettings>().BindConfiguration("Administration").ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<AdministrationSettings>, AdministrationSettingsValidator>();
        builder.Services.AddSingleton<IAuthorizationHandler, AdministrativeAccessHandler>();
        var policy = OrbisAuthentication.IdentityPolicy().AddRequirements(new AdministrativeAccessRequirement()).Build();
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(policy).AddPolicy(AdministrativeAccessHandler.Policy, policy);
        builder.Services.AddOrbisDatabase("OrbisAdministration", "orbis-membership-administration", 8);
        builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();
        builder.Services.AddScoped<ResolveTenantUser>();
        builder.Services.AddScoped<ChangeMemberAccess>();
        builder.Services.AddScoped<IMemberAccessChanges, MemberAccessChanges>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AdministrativeDatabaseCheck>();
        builder.Services.AddHostedService(services => services.GetRequiredService<AdministrativeDatabaseCheck>());
        builder.Services.AddHealthChecks().AddCheck<AdministrativeDatabaseCheck>("database", tags: ["ready"]);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Reserva capacidade do pool para readiness; este limite local não é uma quota distribuída.
            options.AddConcurrencyLimiter("administrative-operations", limiter =>
            {
                limiter.PermitLimit = 4;
                limiter.QueueLimit = 0;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
            options.AddConcurrencyLimiter("readiness", limiter =>
            {
                limiter.PermitLimit = 2;
                limiter.QueueLimit = 0;
            });
        });
        builder.Services.AddRequestTimeouts(options => options.AddPolicy("administrative-command", new RequestTimeoutPolicy
        {
            Timeout = TimeSpan.FromSeconds(5),
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
            WriteTimeoutResponse = async context =>
            {
                context.Response.Headers.RetryAfter = "1";
                await Results.Problem(statusCode: 503, title: "Service temporarily unavailable.").ExecuteAsync(context);
            }
        }));

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/v1"))
            {
                context.Response.OnStarting(() =>
                {
                    // Também vale quando o middleware de exceção limpa e substitui a resposta original.
                    context.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });
                // O bearer administrativo só trafega em TLS; não redirecionar uma credencial já exposta em HTTP.
                if (!context.Request.IsHttps)
                {
                    await Results.Problem(statusCode: 400, title: "HTTPS is required.").ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
            .AllowAnonymous().RequireRateLimiting("readiness");
        app.MapControllers();
        if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
        app.Run();
    }
}
