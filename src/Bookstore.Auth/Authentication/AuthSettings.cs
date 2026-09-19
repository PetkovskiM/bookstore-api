namespace Bookstore.Auth.Authentication;

public sealed class AuthSettings
{
    public string Issuer { get; set; } = "";
    public string[] BrowserRedirectUris { get; set; } = [];

    public static bool IsHttpsAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !uri.Host.Contains('*')
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
