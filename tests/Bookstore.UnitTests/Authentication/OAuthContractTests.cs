using Bookstore.Auth.Authentication;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Bookstore.UnitTests.Authentication;

public sealed class OAuthContractTests
{
    [Fact]
    public void Login_form_policy_allows_only_configured_https_callback_origins()
    {
        var settings = new AuthSettings
        {
            BrowserRedirectUris = ["https://localhost:7100/swagger/oauth2-redirect.html", "https://localhost:7100/another-callback"]
        };

        Assert.Equal("'self' https://localhost:7100", settings.GetFormActionSources());
        Assert.Equal("'self'", new AuthSettings().GetFormActionSources());
    }

    [Theory]
    [InlineData("http://localhost/callback")]
    [InlineData("https://*.example/callback")]
    [InlineData("https://localhost/callback#fragment")]
    [InlineData("https://user:password@localhost/callback")]
    public void Login_form_policy_rejects_unsafe_callback_configuration(string callback)
    {
        var settings = new AuthSettings { BrowserRedirectUris = [callback] };

        Assert.Throws<InvalidOperationException>(() => settings.GetFormActionSources());
    }

    [Fact]
    public void Management_client_requires_a_secret_and_can_only_request_management_tokens()
    {
        var client = OAuthContract.ManagementClient("unit-test-only-placeholder");

        Assert.Equal(ClientTypes.Confidential, client.ClientType);
        Assert.Equal("unit-test-only-placeholder", client.ClientSecret);
        Assert.Empty(client.RedirectUris);
        Assert.True(client.Permissions.SetEquals([
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials,
            Permissions.Prefixes.Scope + OAuthContract.ManageScope]));
    }

    [Fact]
    public void Browser_client_has_no_secret_and_only_the_implicit_search_permissions()
    {
        var callback = new Uri("https://localhost:7200/demo/callback");
        var client = OAuthContract.BrowserClient([callback]);

        Assert.Equal(ClientTypes.Public, client.ClientType);
        Assert.Null(client.ClientSecret);
        Assert.Equal(ConsentTypes.Implicit, client.ConsentType);
        Assert.Equal(callback, Assert.Single(client.RedirectUris));
        Assert.True(client.Permissions.SetEquals([
            Permissions.Endpoints.Authorization,
            Permissions.GrantTypes.Implicit,
            Permissions.ResponseTypes.Token,
            Permissions.Prefixes.Scope + OAuthContract.SearchScope]));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("books.manage", false)]
    [InlineData("books.search books.manage", false)]
    [InlineData("books.search openid", false)]
    [InlineData("books.search offline_access", false)]
    [InlineData("BOOKS.SEARCH", false)]
    [InlineData("books.search", true)]
    public void Search_requires_its_exact_scope_including_rejecting_special_OAuth_scopes(string? scope, bool allowed)
    {
        var request = new OpenIddictRequest { Scope = scope };

        Assert.Equal(allowed, OAuthContract.HasOnlyScope(request, OAuthContract.SearchScope));
    }

    [Theory]
    [InlineData(OAuthContract.ManageScope)]
    [InlineData(OAuthContract.SearchScope)]
    public void Token_principals_only_contain_subject_scope_and_the_bookstore_resource(string scope)
    {
        var principal = OAuthContract.CreatePrincipal("test-subject", scope);

        Assert.Equal("test-subject", principal.GetClaim(Claims.Subject));
        Assert.Equal(scope, Assert.Single(principal.GetScopes()));
        Assert.Equal(OAuthContract.Audience, Assert.Single(principal.GetResources()));
        Assert.Equal(3, principal.Claims.Count());
        Assert.True(principal.Identity!.IsAuthenticated);
    }

    [Fact]
    public void Unsupported_scope_cannot_be_granted()
    {
        Assert.Throws<ArgumentException>(() => OAuthContract.CreatePrincipal("test-subject", "administrator"));
    }

    [Theory]
    [InlineData("https://localhost:7200/", true)]
    [InlineData("https://localhost:7100/swagger/oauth2-redirect.html", true)]
    [InlineData("https://auth.example.test/", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("/demo/callback", false)]
    [InlineData("http://localhost:7200/", false)]
    [InlineData("https://user:password@example.test/", false)]
    [InlineData("https://localhost:7200/#fragment", false)]
    [InlineData("https://localhost:7200/?redirect=elsewhere", false)]
    [InlineData("https://*.example.test/", false)]
    public void Configured_addresses_require_exact_HTTPS_urls_without_credentials(string? address, bool valid)
    {
        Assert.Equal(valid, AuthSettings.IsHttpsAddress(address));
    }
}
