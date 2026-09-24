using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Orbis.Hosting;

public static class OrbisAuthentication
{
    public static AuthorizationPolicyBuilder IdentityPolicy() => new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
        .RequireClaim("sub").RequireClaim("client_id").RequireClaim("jti").RequireClaim("iat");

    public static void AddOrbisJwt(this IServiceCollection services, string section)
    {
        services.AddOptions<AuthenticationSettings>().BindConfiguration(section)
            .Validate(settings => settings.IsValid(), "HTTPS authentication authority and API audience are required.").ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthenticationSettings>>((options, settings) =>
            {
                options.Authority = settings.Value.Authority;
                options.Audience = settings.Value.Audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.BackchannelTimeout = TimeSpan.FromSeconds(3);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Value.Authority,
                    ValidateAudience = true,
                    ValidAudience = settings.Value.Audience,
                    // A audiência separa processos privilegiados; não normalizar barras finais.
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidTypes = ["at+jwt"],
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256]
                };
            });
    }
}
