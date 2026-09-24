using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orbis.Administration.Api;
using Orbis.Application.Memberships;
using Orbis.Domain.Identity;
using Orbis.Domain.Tenancy;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class AdministrativeHttpTests(DatabaseFixture database)
{
    private const Permission Administrator = Permission.ManageMembers | Permission.ReadMembers | Permission.ReadOwnOrders;

    [Fact]
    public async Task AdministrativeRouteAndOpenApiContractsKeepPolicyLimiterAndDeadline()
    {
        await using var app = new AdministrationFactory(database, "Development");
        using var client = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("v1/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, routes.Length);
        foreach (var route in routes)
        {
            Assert.NotNull(route.Metadata.GetMetadata<ControllerActionDescriptor>());
            Assert.Null(route.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Equal(AdministrativeAccessHandler.Policy, route.Metadata.GetMetadata<IAuthorizeData>()!.Policy);
            Assert.Equal("administrative-operations", route.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
            Assert.Equal("administrative-command", route.Metadata.GetMetadata<RequestTimeoutAttribute>()!.PolicyName);
        }
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var paths = document!.RootElement.GetProperty("paths");
        Assert.Equal(3, paths.EnumerateObject().Count());
        foreach (var operation in new[] { "permissions", "suspend", "activate" })
        {
            var contract = paths.GetProperty($"/v1/members/{{id}}/{operation}").GetProperty(operation == "permissions" ? "patch" : "post");
            Assert.True(contract.GetProperty("requestBody").GetProperty("required").GetBoolean());
            Assert.True(contract.GetProperty("responses").TryGetProperty("200", out _));
        }
    }

    [Fact]
    public async Task RealHttpsCommandsPreserveUntouchedFieldsAndCommitOneReceiptPerChange()
    {
        var scope = await SeedAsync();
        var rootPath = new NpgsqlConnectionStringBuilder(database.RuntimeConnection).RootCertificate!;
        using var certificate = HttpsApiTests.HttpsServer.LoadServerCertificate(Path.GetDirectoryName(rootPath)!);
        using var root = X509Certificate2.CreateFromPem(File.ReadAllText(rootPath));
        await using var app = new AdministrationFactory(database, "Performance");
        app.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        app.StartServer();
        using var client = HttpsApiTests.HttpsServer.CreateHttpsClient(app.ClientOptions.BaseAddress, root);
        Authorize(client, app, scope);
        var key = Guid.NewGuid();
        using var first = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl!.NoStore);
        var suspended = (await first.Content.ReadFromJsonAsync<MemberDetails>())!;
        Assert.False(suspended.IsActive);
        Assert.Equal(["ReadOwnOrders"], suspended.Permissions);
        Assert.Equal(2, suspended.Version);
        using var replay = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        using var reused = await Send(client, scope.Target, "activate", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        using var stale = await Send(client, scope.Target, "activate", "{\"expectedVersion\":1}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var permissions = await Send(client, scope.Target, "permissions", "{\"permissionSet\":[\"ReadMembers\"],\"expectedVersion\":2}");
        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
        Assert.False((await permissions.Content.ReadFromJsonAsync<MemberDetails>())!.IsActive);
        using var active = await Send(client, scope.Target, "activate", "{\"expectedVersion\":3}");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        var restored = (await active.Content.ReadFromJsonAsync<MemberDetails>())!;
        Assert.True(restored.IsActive);
        Assert.Equal(["ReadMembers"], restored.Permissions);
        Assert.Equal(4, restored.Version);
        await AssertState(scope, 4, 3);
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("common-audience", 401)]
    [InlineData("wrong-signature", 401)]
    [InlineData("expired", 401)]
    [InlineData("id-token", 401)]
    [InlineData("missing-subject", 403)]
    [InlineData("missing-mfa", 403)]
    [InlineData("weak-mfa", 403)]
    [InlineData("stale-mfa", 403)]
    [InlineData("future-mfa", 403)]
    [InlineData("duplicate-mfa", 403)]
    [InlineData("duplicate-auth-time", 403)]
    [InlineData("invalid-auth-time", 403)]
    [InlineData("multiple-audiences", 403)]
    [InlineData("unapproved-client", 403)]
    public async Task SignedAuthenticationContractRejectsInvalidCredentials(string scenario, int status)
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        client.DefaultRequestHeaders.Authorization = scenario == "anonymous" ? null : new("Bearer", app.Token(scope.Actor, scenario));
        using var response = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        await AssertState(scope, 1, 0);
    }

    [Theory]
    [InlineData("foreign-target")]
    [InlineData("foreign-host")]
    [InlineData("foreign-claim")]
    [InlineData("unknown-host")]
    [InlineData("reader")]
    [InlineData("stronger-target")]
    [InlineData("excess-grant")]
    public async Task PersistedTenantAndDelegationBoundaryRejectsAbuse(string scenario)
    {
        var scope = await SeedAsync(scenario == "stronger-target" ? Permission.ManageOrders : Permission.ReadOwnOrders);
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        if (scenario == "foreign-host") client.DefaultRequestHeaders.Host = database.HostB;
        if (scenario == "unknown-host") client.DefaultRequestHeaders.Host = "unknown.orbis.test";
        if (scenario == "reader") client.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope.Target));
        if (scenario == "foreign-claim") client.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope.Actor, tenant: database.TenantB));
        // Headers/roles não concedem acesso nem substituem o host e as memberships persistidas.
        client.DefaultRequestHeaders.Add("X-Tenant-Id", database.TenantB.ToString());
        using var response = await Send(client, scenario == "foreign-target" ? database.UserB : scope.Target,
            scenario == "excess-grant" ? "permissions" : "suspend", scenario == "excess-grant"
                ? "{\"permissionSet\":[\"ManageOrders\"],\"expectedVersion\":1}" : "{\"expectedVersion\":1}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertState(scope, 1, 0);
    }

    [Theory]
    [InlineData("suspend", "{\"expectedVersion\":1,\"tenantId\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("suspend", "{\"expectedVersion\":1,\"isActive\":true}")]
    [InlineData("suspend", "{\"expectedVersion\":1,\"expectedVersion\":2}")]
    [InlineData("suspend", "{\"expectedVersion\":0}")]
    [InlineData("suspend", "{}")]
    [InlineData("permissions", "{\"permissionSet\":[\"128\"],\"expectedVersion\":1}")]
    [InlineData("permissions", "{\"permissionSet\":[\"ReadMembers\",\"ReadMembers\"],\"expectedVersion\":1}")]
    [InlineData("permissions", "{\"permissionSet\":[null],\"expectedVersion\":1}")]
    [InlineData("permissions", "{\"expectedVersion\":1}")]
    public async Task InvalidOrOverpostedBodiesCannotMutateMembership(string operation, string body)
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        using var response = await Send(client, scope.Target, operation, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        await AssertState(scope, 1, 0);
    }

    [Fact]
    public async Task RevocationAndLastAdministratorProtectionApplyToHttpReplays()
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        using var lastAdmin = await Send(client, scope.Actor, "suspend", "{\"expectedVersion\":1}");
        Assert.Equal(HttpStatusCode.Conflict, lastAdmin.StatusCode);
        var key = Guid.NewGuid();
        using var success = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active=false,version=version+1 WHERE tenant_id={scope.Tenant} AND user_id={scope.Actor}");
        using var denied = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await AssertState(scope, 2, 1);
    }

    [Fact]
    public async Task HostsExposeOnlyTheirOwnControllersAndRejectEachOthersAudience()
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var admin = Client(app, scope);
        using var common = app.Common.CreateClient(new() { BaseAddress = new("https://localhost") });
        common.DefaultRequestHeaders.Host = scope.Host;
        common.DefaultRequestHeaders.Authorization = new("Bearer", app.Token(scope.Actor));
        using var wrongToken = await common.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongToken.StatusCode);
        common.DefaultRequestHeaders.Authorization = new("Bearer", app.Common.Token(scope.Actor, audience: ApiFactory.Audience + "/"));
        using var normalizedAudience = await common.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, normalizedAudience.StatusCode);
        common.DefaultRequestHeaders.Authorization = new("Bearer", app.Common.Token(scope.Actor));
        using var absentAdmin = await Send(common, scope.Target, "suspend", "{\"expectedVersion\":1}");
        using var absentOrders = await admin.GetAsync("/v1/work-orders");
        Assert.Equal(HttpStatusCode.NotFound, absentAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absentOrders.StatusCode);
        using var serviceScope = app.Services.CreateScope();
        Assert.NotNull(serviceScope.ServiceProvider.GetService<ChangeMemberAccess>());
        Assert.DoesNotContain(typeof(AdministrationProgram).Assembly.GetReferencedAssemblies(), a => a.Name == "Orbis.Api");
        Assert.DoesNotContain(typeof(Program).Assembly.GetReferencedAssemblies(), a => a.Name == "Orbis.Administration.Api");
        using var live = await admin.GetAsync("/health/live");
        using var ready = await admin.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task HttpsAndIdempotencyKeyAreRequired()
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        using var noKey = await client.PostAsJsonAsync($"/v1/members/{scope.Target}/suspend", new { expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        using var plaintextClient = app.CreateClient(new() { BaseAddress = new("http://localhost") });
        Authorize(plaintextClient, app, scope);
        using var plaintext = await Send(plaintextClient, scope.Target, "suspend", "{\"expectedVersion\":1}");
        Assert.Equal(HttpStatusCode.BadRequest, plaintext.StatusCode);
        Assert.True(plaintext.Headers.CacheControl!.NoStore);
        await AssertState(scope, 1, 0);
    }

    [Fact]
    public async Task LockedRowTimesOutWithoutPartialMutationAndSameKeyCanRecover()
    {
        var scope = await SeedAsync();
        await using var app = new AdministrationFactory(database);
        using var client = Client(app, scope);
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        await owner.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET version=version WHERE tenant_id={scope.Tenant} AND user_id={scope.Target}");
        var key = Guid.NewGuid();
        var watch = Stopwatch.StartNew();
        using var timeout = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, timeout.StatusCode);
        Assert.True(timeout.Headers.CacheControl!.NoStore);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12));
        await transaction.RollbackAsync();
        await AssertState(scope, 1, 0);
        using var recovered = await Send(client, scope.Target, "suspend", "{\"expectedVersion\":1}", key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        await AssertState(scope, 2, 1);
    }

    private static HttpClient Client(AdministrationFactory app, Scope scope)
    {
        var client = app.CreateClient(new() { BaseAddress = new("https://localhost") });
        Authorize(client, app, scope);
        return client;
    }

    private static void Authorize(HttpClient client, AdministrationFactory app, Scope scope)
    {
        client.DefaultRequestHeaders.Host = scope.Host;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", app.Token(scope.Actor));
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, Guid member, string operation, string body, Guid? key = null)
    {
        using var request = new HttpRequestMessage(operation == "permissions" ? HttpMethod.Patch : HttpMethod.Post, $"/v1/members/{member}/{operation}")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await client.SendAsync(request);
    }

    private async Task<Scope> SeedAsync(Permission targetPermissions = Permission.ReadOwnOrders)
    {
        var scope = new Scope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var directory = database.CreateDirectoryContext(admin: true);
        var tenant = new Tenant(scope.Tenant, "Administrative HTTP test");
        tenant.Activate();
        var domain = new TenantDomain(scope.Tenant, scope.Host);
        domain.MarkVerified();
        directory.AddRange(tenant, domain);
        foreach (var user in new[] { scope.Actor, scope.Target })
            directory.AddRange(new UserAccount(user), new ExternalIdentity(user, ApiFactory.Issuer, user.ToString()));
        await directory.SaveChangesAsync();
        await using var owner = database.CreateContext(scope.Tenant, admin: true);
        await using var transaction = await owner.BeginTenantTransactionAsync();
        owner.AddRange(new Membership(scope.Tenant, scope.Actor, Administrator), new Membership(scope.Tenant, scope.Target, targetPermissions));
        await owner.SaveChangesAsync();
        await transaction.CommitAsync();
        return scope;
    }

    private async Task AssertState(Scope scope, long version, int receipts)
    {
        await using var context = database.CreateContext(scope.Tenant, connection: database.MembershipConnection);
        await using var transaction = await context.BeginTenantTransactionAsync();
        Assert.Equal(version, (await context.Memberships.SingleAsync(m => m.UserId == scope.Target)).Version);
        Assert.Equal(receipts, await context.MembershipAccessChanges.CountAsync());
    }

    private sealed record Scope(Guid Tenant, Guid Actor, Guid Target)
    {
        public string Host => $"administrative-http-{Tenant:N}.orbis.test";
    }
}
