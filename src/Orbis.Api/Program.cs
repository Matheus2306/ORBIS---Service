using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck("foundation", () => HealthCheckResult.Unhealthy("Business dependencies are not configured."), ["ready"]);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness não pode declarar prontidão de negócio enquanto persistência/identidade não estiverem conectadas.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.Run();

public partial class Program;
