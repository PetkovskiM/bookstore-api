namespace Bookstore.Api.Security;

public sealed class JwtSettings
{
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "bookstore-api";
    public string? BackchannelHost { get; set; }

    public bool IsValid() =>
        Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !string.IsNullOrWhiteSpace(Audience)
        && (BackchannelHost is null || Uri.CheckHostName(BackchannelHost) is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6);
}
