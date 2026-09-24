using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Orbis.Hosting;

namespace Orbis.Administration.Api;

public sealed class AdministrationSettings
{
    public string CommonApiAudience { get; set; } = string.Empty;
    public string RequiredAcr { get; set; } = string.Empty;
    public string[] AllowedClientIds { get; set; } = [];
    public int MaximumAuthenticationAgeSeconds { get; set; } = 900;

    public bool IsValid(AuthenticationSettings authentication) => !string.IsNullOrWhiteSpace(CommonApiAudience) &&
        CommonApiAudience != authentication.Audience && RequiredAcr.Length is > 0 and <= 256 &&
        !RequiredAcr.Any(char.IsWhiteSpace) && MaximumAuthenticationAgeSeconds is >= 60 and <= 900 &&
        AllowedClientIds.Length is > 0 and <= 16 && AllowedClientIds.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 256) &&
        AllowedClientIds.Distinct(StringComparer.Ordinal).Count() == AllowedClientIds.Length;
}

public sealed class AdministrativeAccessRequirement : IAuthorizationRequirement;

public sealed class AdministrativeAccessHandler(IOptions<AdministrationSettings> settings,
    IOptions<AuthenticationSettings> authentication, TimeProvider clock) : AuthorizationHandler<AdministrativeAccessRequirement>
{
    public const string Policy = "membership-administration";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministrativeAccessRequirement requirement)
    {
        var user = context.User;
        var current = settings.Value;
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        // ACR é um contrato explícito com o IdP: nenhuma role/amr/header do cliente substitui essa comprovação assinada.
        if (Single(user, "aud") == authentication.Value.Audience && Single(user, "acr") == current.RequiredAcr &&
            current.AllowedClientIds.Contains(Single(user, "client_id"), StringComparer.Ordinal) &&
            Single(user, "sub") is { Length: > 0 } && Single(user, "jti") is { Length: > 0 } &&
            long.TryParse(Single(user, "auth_time"), NumberStyles.None, CultureInfo.InvariantCulture, out var authenticatedAt) &&
            long.TryParse(Single(user, "iat"), NumberStyles.None, CultureInfo.InvariantCulture, out var issuedAt) &&
            authenticatedAt > 0 && issuedAt > 0 && authenticatedAt <= now + 30 && issuedAt <= now + 30 &&
            authenticatedAt >= now - current.MaximumAuthenticationAgeSeconds && authenticatedAt <= issuedAt + 30)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static string? Single(ClaimsPrincipal user, string type)
    {
        var values = user.FindAll(type).Take(2).ToArray();
        return values.Length == 1 ? values[0].Value : null;
    }
}
