namespace Bookstore.Api.Security;

public sealed class JwtSettings
{
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "bookstore-api";

    public bool IsValid() =>
        Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !string.IsNullOrWhiteSpace(Audience);
}
