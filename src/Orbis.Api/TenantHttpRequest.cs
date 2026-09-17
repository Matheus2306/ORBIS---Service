using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Orbis.Application.Tenancy;

namespace Orbis.Api;

public static class TenantHttpRequest
{
    public static bool TryRead(HttpContext context, [NotNullWhen(true)] out TenantRequest? request)
    {
        request = null;
        var claims = context.User.FindAll("tenant_id").ToArray();
        Guid? tenant = null;
        if (claims.Length > 1 || (claims.Length == 1 && !Guid.TryParse(claims[0].Value, out _))) return false;
        if (claims.Length == 1) tenant = Guid.Parse(claims[0].Value);
        request = new(context.Request.Host.Host, context.User.FindFirst("iss")?.Value ?? string.Empty,
            context.User.FindFirst("sub")?.Value ?? string.Empty, tenant);
        return true;
    }
}

// Campos de autoridade adicionais são rejeitados, inclusive TenantId/CustomerId/Status no payload.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestOrderBody(string? Description);
