using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbis.Administration.Api;

namespace Orbis.IntegrationTests;

public sealed class AdministrationFactory(DatabaseFixture database, string environment = "Testing") : WebApplicationFactory<AdministrationProgram>
{
    public const string Audience = "orbis-administration-tests";
    public const string Acr = "urn:orbis:test:mfa";
    public ApiFactory Common { get; } = new(database, environment);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Administration:Authentication:Authority"] = ApiFactory.Issuer,
            ["Administration:Authentication:Audience"] = Audience,
            ["Administration:CommonApiAudience"] = ApiFactory.Audience,
            ["Administration:RequiredAcr"] = Acr,
            ["Administration:AllowedClientIds:0"] = "integration-tests",
            ["ConnectionStrings:OrbisAdministration"] = database.MembershipConnection
        }));
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, Common.ConfigureSigningKeys));
    }

    public string Token(Guid user, string? scenario = null, Guid? tenant = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new List<Claim> { new("acr", scenario == "weak-mfa" ? "password" : Acr),
            new("auth_time", (now + (scenario == "stale-mfa" ? -1000 : scenario == "future-mfa" ? 120 : 0)).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64) };
        if (scenario == "missing-mfa") claims.Clear();
        if (scenario == "duplicate-mfa") claims.Add(new("acr", Acr));
        if (scenario == "duplicate-auth-time") claims.Add(new("auth_time", now.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        if (scenario == "multiple-audiences") claims.Add(new("aud", ApiFactory.Audience));
        if (scenario == "unapproved-client") claims.Add(new("client_id", "unapproved-client"));
        if (scenario == "invalid-auth-time") { claims.RemoveAll(c => c.Type == "auth_time"); claims.Add(new("auth_time", "never")); }
        return Common.Token(user, audience: scenario == "common-audience" ? ApiFactory.Audience : Audience, tenantId: tenant,
            expired: scenario == "expired", wrongKey: scenario == "wrong-signature", type: scenario == "id-token" ? "JWT" : "at+jwt",
            omittedClaim: scenario == "unapproved-client" ? "client_id" : scenario == "missing-subject" ? "sub" : null, additionalClaims: claims);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Common.DisposeAsync();
    }
}
