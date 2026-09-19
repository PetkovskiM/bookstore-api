using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Bookstore.Auth.Authentication;

public static class OAuthContract
{
    public const string Audience = "bookstore-api";
    public const string ManageScope = "books.manage";
    public const string SearchScope = "books.search";
    public const string ManagementClientId = "bookstore-management";
    public const string BrowserClientId = "bookstore-browser";
    public const string DemoUserName = "demo@bookstore.local";

    public static bool HasOnlyScope(OpenIddictRequest request, string scope)
    {
        var scopes = request.GetScopes();
        return scopes.Length == 1 && scopes[0] == scope;
    }

    public static ClaimsPrincipal CreatePrincipal(string subject, string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (scope is not (ManageScope or SearchScope))
        {
            throw new ArgumentException("Unsupported book scope.", nameof(scope));
        }

        // Build a new identity: never copy Identity cookies, security stamps, or passwords into tokens.
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
        identity.SetClaim(Claims.Subject, subject);
        identity.SetScopes(scope);
        identity.SetResources(Audience);
        return new ClaimsPrincipal(identity);
    }

    public static OpenIddictApplicationDescriptor ManagementClient(string secret) => new()
    {
        ClientId = ManagementClientId,
        ClientType = ClientTypes.Confidential,
        ClientSecret = secret,
        DisplayName = "Bookstore management",
        Permissions =
        {
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials,
            Permissions.Prefixes.Scope + ManageScope
        }
    };

    public static OpenIddictApplicationDescriptor BrowserClient(IEnumerable<Uri> redirectUris)
    {
        var client = new OpenIddictApplicationDescriptor
        {
            ClientId = BrowserClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Bookstore browser",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.GrantTypes.Implicit,
                Permissions.ResponseTypes.Token,
                Permissions.Prefixes.Scope + SearchScope
            }
        };
        client.RedirectUris.UnionWith(redirectUris);
        return client;
    }
}
