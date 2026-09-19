namespace Bookstore.Auth.Authentication;

public sealed class AuthSettings
{
    public string Issuer { get; set; } = "";
    public string[] BrowserRedirectUris { get; set; } = [];

    public string GetFormActionSources()
    {
        if (!BrowserRedirectUris.All(IsHttpsAddress))
        {
            throw new InvalidOperationException("Configure exact HTTPS Auth:BrowserRedirectUris. See README.md.");
        }

        // Chromium applies form-action to the redirect chain after a successful login POST.
        // Only configured HTTPS callback origins may receive that navigation; OpenIddict
        // separately validates the complete registered redirect URI before issuing a token.
        return string.Join(' ', BrowserRedirectUris.Select(address => new Uri(address).GetLeftPart(UriPartial.Authority))
            .Prepend("'self'").Distinct(StringComparer.Ordinal));
    }

    public static bool IsHttpsAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !uri.Host.Contains('*')
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
