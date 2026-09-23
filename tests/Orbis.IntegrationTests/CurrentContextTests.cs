using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orbis.Application.Tenancy;
using Orbis.Domain.Identity;
using Orbis.Infrastructure.Persistence;
using Orbis.Infrastructure.Queries;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class CurrentContextTests(DatabaseFixture database)
{
    private static readonly string[] Paths = ["/v1/me", "/v1/me/permissions", "/v1/tenant"];

    [Fact]
    public async Task ContextExposesOnlyCurrentIdentityTenantAndPersistedPermissions()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        using var me = await client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True(me.Headers.CacheControl?.NoStore);
        using var json = await me.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(new[] { "tenantId", "userId" }, json!.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(database.UserA, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(database.TenantA, json.RootElement.GetProperty("tenantId").GetGuid());
        var permissions = await client.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions");
        Assert.Equal(new[] { "ReadOwnOrders", "CreateOrders" }, permissions!.Permissions);
        Assert.Equal(database.TenantA, permissions.TenantId);
        Assert.Equal(database.UserA, permissions.UserId);
        var tenant = await client.GetFromJsonAsync<CurrentTenantDetails>("/v1/tenant");
        Assert.Equal(database.TenantA, tenant!.Id);
        Assert.Equal("Synthetic tenant", tenant.Name);
    }

    [Theory]
    [InlineData("/v1/me")]
    [InlineData("/v1/me/permissions")]
    [InlineData("/v1/tenant")]
    public async Task ForeignOrUnverifiedTenantDoesNotExposeContext(string path)
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostB, database.UserA);
        using var foreign = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.DoesNotContain(database.TenantB.ToString(), await foreign.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        foreach (var host in new[] { database.UnverifiedHost, database.HostA + ".attacker.example" })
        {
            using var unknownClient = Client(application, host, database.UserA);
            using var unknown = await unknownClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
    }

    [Fact]
    public async Task ClaimsAndClientSelectorsCannotOverrideVerifiedHost()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, database.UserA);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", database.TenantB.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", database.HostB);
        var me = await client.GetFromJsonAsync<CurrentUserDetails>($"/v1/me?tenantId={database.TenantB}&userId={database.UserB}");
        Assert.Equal(new CurrentUserDetails(database.UserA, database.TenantA), me);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(database.UserA, tenantId: database.TenantB));
        foreach (var path in Paths)
        {
            using var denied = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
    }

    [Fact]
    public async Task SameGlobalUserGetsOnlyThePermissionsOfTheSelectedMembership()
    {
        await using var application = new ApiFactory(database);
        using var a = Client(application, database.HostA, database.MultiTenantUser);
        using var b = Client(application, database.HostB, database.MultiTenantUser);
        var permissionsA = await a.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions");
        var permissionsB = await b.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions");
        Assert.Equal(database.TenantA, permissionsA!.TenantId);
        Assert.Equal(database.TenantB, permissionsB!.TenantId);
        Assert.Contains("ReadAllOrders", permissionsA.Permissions);
        Assert.DoesNotContain("ReadAllOrders", permissionsB.Permissions);
        Assert.Equal(permissionsA.UserId, permissionsB.UserId);
    }

    [Fact]
    public async Task ActiveMemberWithNoBusinessPermissionsCanReadOwnContextButCannotReadOrders()
    {
        var userId = await CreateUser(Permission.None);
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, userId);
        foreach (var path in Paths)
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        Assert.Empty((await client.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions"))!.Permissions);
        using var orders = await client.GetAsync("/v1/work-orders");
        Assert.Equal(HttpStatusCode.NotFound, orders.StatusCode);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("user")]
    public async Task RevocationIsObservedWithAnAlreadyIssuedToken(string boundary)
    {
        var userId = await CreateUser(Permission.ReadOwnOrders);
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, userId);
        using var before = await client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        if (boundary == "membership")
        {
            await using var admin = database.CreateContext(database.TenantA, admin: true);
            await using var transaction = await admin.BeginTenantTransactionAsync();
            var member = await admin.Memberships.SingleAsync(member => member.UserId == userId);
            member.Suspend();
            await admin.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        else
        {
            await using var directory = database.CreateDirectoryContext(admin: true);
            var user = await directory.Users.SingleAsync(user => user.Id == userId);
            user.Suspend();
            await directory.SaveChangesAsync();
        }
        foreach (var path in Paths)
        {
            using var denied = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
    }

    [Fact]
    public async Task ChangedPermissionsAreReadFromDatabaseWithoutWaitingForTokenExpiry()
    {
        var userId = await CreateUser(Permission.ReadOwnOrders);
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, userId);
        Assert.Single((await client.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions"))!.Permissions);
        await using var admin = database.CreateContext(database.TenantA, admin: true);
        await using var transaction = await admin.BeginTenantTransactionAsync();
        await admin.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET permissions=0 WHERE user_id={userId}");
        await transaction.CommitAsync();
        Assert.Empty((await client.GetFromJsonAsync<CurrentPermissions>("/v1/me/permissions"))!.Permissions);
    }

    [Fact]
    public async Task ContextReaderRejectsActorWithoutMembershipInItsTenant()
    {
        await using var directory = database.CreateDirectoryContext();
        var reader = new CurrentContextReader(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(database.RuntimeConnection).Options, directory);
        Assert.Null(await reader.FindAsync(new TenantUser(database.TenantA, database.UserB), CancellationToken.None));
        Assert.Null(await reader.FindAsync(new TenantUser(database.TenantB, database.UserA), CancellationToken.None));
    }

    [Fact]
    public async Task SuspendedTenantAndUnregisteredIdentityCannotReadContext()
    {
        await using var application = new ApiFactory(database);
        using var unknown = Client(application, database.HostA, Guid.NewGuid());
        using var registered = Client(application, database.HostA, database.UserA);
        await using var directory = database.CreateDirectoryContext(admin: true);
        var tenant = await directory.Tenants.SingleAsync(tenant => tenant.Id == database.TenantA);
        foreach (var path in Paths)
        {
            using var denied = await unknown.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        tenant.Suspend();
        await directory.SaveChangesAsync();
        try
        {
            foreach (var path in Paths)
            {
                using var denied = await registered.GetAsync(path);
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            }
        }
        finally { tenant.Activate(); await directory.SaveChangesAsync(); }
    }

    private async Task<Guid> CreateUser(Permission permissions)
    {
        var id = Guid.NewGuid();
        await using var directory = database.CreateDirectoryContext(admin: true);
        directory.Users.Add(new UserAccount(id));
        directory.Identities.Add(new ExternalIdentity(id, ApiFactory.Issuer, id.ToString()));
        await directory.SaveChangesAsync();
        await using var context = database.CreateContext(database.TenantA, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        context.Memberships.Add(new Membership(database.TenantA, id, permissions));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return id;
    }

    private static HttpClient Client(ApiFactory application, string host, Guid user)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{host}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", application.Token(user));
        return client;
    }

    [Fact]
    public async Task ContextWorksWithASinglePooledConnectionAndReturnsItAfterReading()
    {
        var settings = new NpgsqlConnectionStringBuilder(database.RuntimeConnection) { MaxPoolSize = 1, Timeout = 2 };
        await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
        await using var directory = new DirectoryDbContext(new DbContextOptionsBuilder<DirectoryDbContext>().UseNpgsql(source).Options);
        var reader = new CurrentContextReader(new DbContextOptionsBuilder<TenantDbContext>().UseNpgsql(source).Options, directory);
        var query = new ReadCurrentContext(new ResolveTenantUser(new TenantDirectory(directory)), reader);
        var result = await query.ExecuteAsync(new(database.HostA, ApiFactory.Issuer, database.UserA.ToString(), null), CancellationToken.None);
        Assert.Equal(database.TenantA, result!.TenantId);
        await using var released = await source.OpenConnectionAsync();
        Assert.Equal(System.Data.ConnectionState.Open, released.State);
    }
}
