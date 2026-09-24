using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Orbis.Application.Tenancy;

namespace Orbis.Hosting;

public static class TenantHttpRequest
{
    public static bool TryReadIdempotencyKey(HttpContext context, out Guid key)
    {
        key = Guid.Empty;
        var values = context.Request.Headers["Idempotency-Key"];
        return values.Count == 1 && Guid.TryParseExact(values[0], "D", out key) && key != Guid.Empty;
    }

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
