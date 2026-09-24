using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Orbis.Api;
using Orbis.Application.Memberships;
using Orbis.Application.Tenancy;
using Orbis.Application.WorkOrders;
using Orbis.Hosting;
using Orbis.Infrastructure.Queries;

var builder = WebApplication.CreateBuilder(args);
builder.AddOrbisHttp();
builder.Services.AddOpenApi();
builder.Services.AddOrbisJwt("Authentication");
var tenantAccessPolicy = OrbisAuthentication.IdentityPolicy().Build();
// Uma policy nomeada mantém as claims obrigatórias mesmo com autorização explícita nos controllers.
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(tenantAccessPolicy)
    .AddPolicy(ApiPolicies.TenantAccess, tenantAccessPolicy);

builder.Services.AddOrbisDatabase("Orbis", "orbis-runtime", 20);
builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();
builder.Services.AddScoped<IWorkOrderReader, WorkOrderReader>();
builder.Services.AddScoped<ReadWorkOrder>();
builder.Services.AddScoped<ListWorkOrders>();
builder.Services.AddScoped<ResolveTenantUser>();
builder.Services.AddScoped<ReadCurrentContext>();
builder.Services.AddScoped<ICurrentContextReader, CurrentContextReader>();
builder.Services.AddScoped<ReadMember>();
builder.Services.AddScoped<ListMembers>();
builder.Services.AddScoped<IMemberReader, MemberReader>();
builder.Services.AddScoped<CreateWorkOrder>();
builder.Services.AddScoped<IWorkOrderCreator, WorkOrderCreator>();
builder.Services.AddScoped<TransitionWorkOrder>();
builder.Services.AddScoped<IWorkOrderTransitions, WorkOrderTransitions>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RuntimeDatabaseCheck>();
builder.Services.AddHostedService(services => services.GetRequiredService<RuntimeDatabaseCheck>());
builder.Services.AddHealthChecks().AddCheck<RuntimeDatabaseCheck>("database", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Limite de proteção por instância, não quota comercial nem proteção DDoS distribuída.
    options.AddConcurrencyLimiter("database-operations", limiter =>
    {
        limiter.PermitLimit = 16;
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.AddConcurrencyLimiter("readiness", limiter =>
    {
        limiter.PermitLimit = 4;
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous().RequireRateLimiting("readiness");
app.MapControllers();
if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();
app.Run();

public partial class Program;
