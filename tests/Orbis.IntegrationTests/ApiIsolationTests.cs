using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Orbis.Application.WorkOrders;

namespace Orbis.IntegrationTests;

[Collection("Database")]
public sealed class ApiIsolationTests(DatabaseFixture database)
{
    [Fact]
    public async Task ValidTokenCanReadOwnOrderButCannotReadCrossTenantOrAnotherCustomer()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, application.Token(database.UserA));
        var own = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.True(own.Headers.CacheControl?.NoStore);
        Assert.Equal(database.OrderA, (await own.Content.ReadFromJsonAsync<OrderDetails>())!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OrderB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OtherOrderA}")).StatusCode);
        using var otherHost = Client(application, database.HostB, application.Token(database.UserA));
        Assert.Equal(HttpStatusCode.NotFound, (await otherHost.GetAsync($"/v1/work-orders/{database.OrderB}")).StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("id-token")]
    [InlineData("missing")]
    public async Task AuthenticationRejectsInvalidTokens(string flaw)
    {
        await using var application = new ApiFactory(database);
        var token = flaw == "missing" ? null : application.Token(database.UserA,
            issuer: flaw == "issuer" ? "https://attacker.example" : null,
            audience: flaw == "audience" ? "other-api" : null, expired: flaw == "expired",
            wrongKey: flaw == "signature", type: flaw == "id-token" ? "JWT" : "at+jwt");
        using var client = Client(application, database.HostA, token);
        var response = await client.GetAsync($"/v1/work-orders/{database.OrderA}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
    }

    [Fact]
    public async Task SameGlobalIdentityHasIndependentPermissionsInEachTenant()
    {
        await using var application = new ApiFactory(database);
        var token = application.Token(database.MultiTenantUser);
        using var tenantA = Client(application, database.HostA, token);
        using var tenantB = Client(application, database.HostB, token);
        Assert.Equal(HttpStatusCode.OK, (await tenantA.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.GetAsync($"/v1/work-orders/{database.OrderB}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tenantB.GetAsync($"/v1/work-orders/{database.MultiTenantOrderB}")).StatusCode);
    }

    [Fact]
    public async Task ClientHeadersCannotSelectTenantOrOverrideHost()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, application.Token(database.UserA));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", database.TenantB.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", database.HostB);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OrderB}")).StatusCode);
    }

    [Fact]
    public async Task ConflictingTenantClaimAndUnverifiedOrUnknownDomainsAreDenied()
    {
        await using var application = new ApiFactory(database);
        using var conflict = Client(application, database.HostA, application.Token(database.UserA, tenantId: database.TenantB));
        Assert.Equal(HttpStatusCode.NotFound, (await conflict.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        foreach (var host in new[] { database.UnverifiedHost, database.HostA + ".attacker.example" })
        {
            using var client = Client(application, host, application.Token(database.UserA));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        }
    }

    [Fact]
    public async Task MembershipRevocationTakesEffectWithTheSamePreviouslyValidToken()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, application.Token(database.UserA));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        await SetMembershipActive(false);
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode);
        }
        finally { await SetMembershipActive(true); }
    }

    [Fact]
    public async Task TenantSuspensionTakesEffectWithoutTokenExpiration()
    {
        await using var application = new ApiFactory(database);
        using var client = Client(application, database.HostA, application.Token(database.UserA));
        await using var directory = database.CreateDirectoryContext(admin: true);
        var tenant = await directory.Tenants.SingleAsync(x => x.Id == database.TenantA);
        tenant.Suspend();
        await directory.SaveChangesAsync();
        try { Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/work-orders/{database.OrderA}")).StatusCode); }
        finally { tenant.Activate(); await directory.SaveChangesAsync(); }
    }

    private static HttpClient Client(ApiFactory application, string host, string? token)
    {
        var client = application.CreateClient();
        client.BaseAddress = new Uri($"https://{host}");
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SetMembershipActive(bool active)
    {
        await using var context = database.CreateContext(database.TenantA, admin: true);
        await using var transaction = await context.BeginTenantTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE memberships SET is_active={active} WHERE user_id={database.UserA}");
        await transaction.CommitAsync();
    }
}
