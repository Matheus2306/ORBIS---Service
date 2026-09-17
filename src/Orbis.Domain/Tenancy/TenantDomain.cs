using System.Globalization;

namespace Orbis.Domain.Tenancy;

public sealed class TenantDomain
{
    public string Host { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public bool IsVerified { get; private set; }

    private TenantDomain() { }

    public TenantDomain(Guid tenantId, string host)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity is required.", nameof(tenantId));
        TenantId = tenantId;
        Host = NormalizeHost(host);
    }

    // Somente o workflow privilegiado de provisionamento pode registrar prova de posse.
    public void MarkVerified() => IsVerified = true;

    public static string NormalizeHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (host.Length > 254 || host.Any(char.IsWhiteSpace)) throw new ArgumentException("Invalid DNS host.", nameof(host));
        var candidate = host.EndsWith('.') ? host[..^1] : host;
        var ascii = new IdnMapping().GetAscii(candidate).ToLowerInvariant();
        if (ascii.Length > 253 || Uri.CheckHostName(ascii) != UriHostNameType.Dns ||
            ascii.Split('.').Any(label => label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-' ||
                label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
            throw new ArgumentException("Invalid DNS host.", nameof(host));
        return ascii;
    }
}
