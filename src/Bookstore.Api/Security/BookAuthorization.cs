using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bookstore.Api.Security;

public static class BookAuthorization
{
    public const string Manage = "books.manage";
    public const string Search = "books.search";

    public static IServiceCollection AddBookAuthorization(this IServiceCollection services)
    {
        return services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(Manage, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, Manage)));
            options.AddPolicy(Search, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, Search)));
        });
    }

    private static bool HasScope(ClaimsPrincipal user, string requiredScope) =>
        user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
}
