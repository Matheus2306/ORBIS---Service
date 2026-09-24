namespace Orbis.Hosting;

public sealed class AuthenticationSettings
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public bool IsValid() => Uri.TryCreate(Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        !string.IsNullOrWhiteSpace(Audience);
}
