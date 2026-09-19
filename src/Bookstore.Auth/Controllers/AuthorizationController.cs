using Bookstore.Auth.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OAuthErrors = OpenIddict.Abstractions.OpenIddictConstants.Errors;

namespace Bookstore.Auth.Controllers;

public sealed class AuthorizationController(SignInManager<IdentityUser> signIn) : Controller
{
    [HttpPost("/connect/token")]
    [IgnoreAntiforgeryToken] // A protocol endpoint authenticates clients, not an Identity browser form.
    public IActionResult Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        // OpenIddict has already checked the client secret and endpoint/grant/scope permissions.
        if (!request.IsClientCredentialsGrantType() || request.ClientId != OAuthContract.ManagementClientId)
        {
            return Reject(OAuthErrors.UnauthorizedClient, "This client cannot use the requested grant.");
        }
        if (!OAuthContract.HasOnlyScope(request, OAuthContract.ManageScope))
        {
            return Reject(OAuthErrors.InvalidScope, "Request only books.manage.");
        }
        return SignIn(OAuthContract.CreatePrincipal(request.ClientId, OAuthContract.ManageScope),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;
        if (!request.IsImplicitFlow() || request.ClientId != OAuthContract.BrowserClientId)
        {
            return Reject(OAuthErrors.UnauthorizedClient, "This client cannot use the requested flow.");
        }
        if (!OAuthContract.HasOnlyScope(request, OAuthContract.SearchScope))
        {
            return Reject(OAuthErrors.InvalidScope, "Request only books.search.");
        }

        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = authentication.Succeeded
            ? await signIn.ValidateSecurityStampAsync(authentication.Principal)
            : null;
        var requiresLogin = user is null || request.HasPromptValue(PromptValues.Login)
            || (request.MaxAge is long maxAge &&
                (authentication.Properties?.IssuedUtc is not DateTimeOffset issuedAt
                    || (DateTimeOffset.UtcNow - issuedAt).TotalSeconds >= maxAge));
        if (requiresLogin)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Reject(OAuthErrors.LoginRequired, "Sign in before requesting search access.");
            }
            await signIn.SignOutAsync();
            // Consume the reauthentication request so returning from login does not create a redirect loop.
            var query = QueryString.Create(Request.Query.Where(pair => pair.Key is not ("prompt" or "max_age")));
            return Challenge(new AuthenticationProperties { RedirectUri = Request.PathBase + Request.Path + query },
                IdentityConstants.ApplicationScheme);
        }

        return SignIn(OAuthContract.CreatePrincipal(user!.Id, OAuthContract.SearchScope),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ForbidResult Reject(string error, string description) => Forbid(new AuthenticationProperties
    {
        Items =
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }
    }, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
