using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Orbis.Api;
using Orbis.Application.WorkOrders;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DependencyExceptionHandler>();
builder.Services.AddOpenApi();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024);

builder.Services.AddOptions<AuthenticationSettings>().BindConfiguration("Authentication")
    .Validate(settings => settings.IsValid(), "HTTPS authentication authority and API audience are required.").ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationSettings>>((options, settings) =>
{
    options.Authority = settings.Value.Authority;
    options.Audience = settings.Value.Audience;
    options.RequireHttpsMetadata = true;
    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = settings.Value.Authority,
        ValidateAudience = true,
        ValidAudience = settings.Value.Audience,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidTypes = ["at+jwt"],
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256]
    };
});
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().RequireClaim("sub").RequireClaim("client_id").RequireClaim("jti").RequireClaim("iat").Build());

builder.Services.AddSingleton(services =>
{
    var connection = services.GetRequiredService<IConfiguration>().GetConnectionString("Orbis");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Database connection is required.");
    var databaseSettings = new NpgsqlConnectionStringBuilder(connection);
    var environment = services.GetRequiredService<IHostEnvironment>();
    if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && databaseSettings.SslMode != SslMode.VerifyFull)
        throw new InvalidOperationException("Database TLS with full certificate verification is required outside Development and Testing.");
    databaseSettings.MaxPoolSize = Math.Min(databaseSettings.MaxPoolSize, 20);
    databaseSettings.Timeout = 5;
    databaseSettings.CommandTimeout = 5;
    return NpgsqlDataSource.Create(databaseSettings.ConnectionString);
});
builder.Services.AddDbContext<DirectoryDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>(), provider =>
        provider.MigrationsHistoryTable("__DirectoryMigrationsHistory", "directory")));
builder.Services.AddSingleton(services => new DbContextOptionsBuilder<TenantDbContext>()
    .UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()).Options);
builder.Services.AddScoped<ITenantDirectory, TenantDirectory>();
builder.Services.AddScoped<IWorkOrderReader, WorkOrderReader>();
builder.Services.AddScoped<ReadWorkOrder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RuntimeDatabaseCheck>();
builder.Services.AddHostedService(services => services.GetRequiredService<RuntimeDatabaseCheck>());
builder.Services.AddHealthChecks().AddCheck<RuntimeDatabaseCheck>("database", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Limite de proteção por instância, não quota comercial nem proteção DDoS distribuída.
    options.AddConcurrencyLimiter("database-reads", limiter =>
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
app.MapGet("/v1/work-orders/{id:guid}", async (Guid id, HttpContext context, ReadWorkOrder query, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var tenantClaims = context.User.FindAll("tenant_id").ToArray();
    Guid? claimedTenant = null;
    if (tenantClaims.Length > 1 || (tenantClaims.Length == 1 && !Guid.TryParse(tenantClaims[0].Value, out _)))
        return Results.NotFound();
    if (tenantClaims.Length == 1) claimedTenant = Guid.Parse(tenantClaims[0].Value);
    var order = await query.ExecuteAsync(context.Request.Host.Host,
        context.User.FindFirst("iss")?.Value ?? string.Empty, context.User.FindFirst("sub")?.Value ?? string.Empty,
        claimedTenant, id, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
}).RequireRateLimiting("database-reads");
if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();
app.Run();

public partial class Program;
