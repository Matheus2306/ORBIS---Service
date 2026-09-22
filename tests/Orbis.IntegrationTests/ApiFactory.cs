using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Orbis.IntegrationTests;

public sealed class ApiFactory(DatabaseFixture database, string environment = "Testing") : WebApplicationFactory<Program>
{
    public const string Issuer = "https://identity.orbis.test";
    public const string Audience = "orbis-api-tests";
    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly string _keyId = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["Authentication:Audience"] = Audience,
            ["ConnectionStrings:Orbis"] = database.RuntimeConnection
        }));
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            // Somente o endpoint de discovery é substituído; assinatura e todas as validações JWT permanecem reais.
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(new RsaSecurityKey(_signingKey.ExportParameters(false)) { KeyId = _keyId });
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        }));
    }

    public string Token(Guid userId, string? issuer = null, string? audience = null, Guid? tenantId = null,
        bool expired = false, string type = "at+jwt", bool wrongKey = false, string? omittedClaim = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()), new("client_id", "integration-tests"), new("jti", Guid.NewGuid().ToString()),
            new("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        if (omittedClaim is not null) claims.RemoveAll(claim => claim.Type == omittedClaim);
        if (tenantId.HasValue) claims.Add(new Claim("tenant_id", tenantId.ToString()!));
        using var invalidKey = wrongKey ? RSA.Create(2048) : null;
        var key = new RsaSecurityKey(invalidKey ?? _signingKey) { KeyId = wrongKey ? "unknown" : _keyId };
        var token = new JwtSecurityToken(issuer ?? Issuer, audience ?? Audience, claims, now.AddMinutes(-10),
            now.AddMinutes(expired ? -5 : 5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = type;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _signingKey.Dispose();
    }
}
