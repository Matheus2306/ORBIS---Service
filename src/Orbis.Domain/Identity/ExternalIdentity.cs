namespace Orbis.Domain.Identity;

public sealed class ExternalIdentity
{
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }

    private ExternalIdentity() { }

    public ExternalIdentity(Guid userId, string issuer, string subject)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User identity is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (issuer.Length > 512 || issuer.Any(character => !char.IsAscii(character)) ||
            !Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Issuer must be an exact HTTPS identifier.", nameof(issuer));
        if (subject.Length > 256) throw new ArgumentOutOfRangeException(nameof(subject));
        UserId = userId;
        // Subject é opaco e case-sensitive; normalizá-lo pode unir contas diferentes.
        Issuer = issuer;
        Subject = subject;
    }
}
