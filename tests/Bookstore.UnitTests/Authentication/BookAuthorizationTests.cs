using System.Reflection;
using System.Security.Claims;
using Bookstore.Api.Controllers;
using Bookstore.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bookstore.UnitTests.Authentication;

public sealed class BookAuthorizationTests
{
    [Theory]
    [InlineData("books.manage", "books.manage", true, true)]
    [InlineData("books.search", "books.search", true, true)]
    [InlineData("books.manage", "books.search", true, false)]
    [InlineData("books.search", "books.manage", true, false)]
    [InlineData("books.manage", "books.manage", false, false)]
    [InlineData("books.search", "", true, false)]
    [InlineData("books.search", "BOOKS.SEARCH", true, false)]
    [InlineData("books.search", "books.search.extra", true, false)]
    [InlineData("books.search", "unrelated books.search another", true, true)]
    public async Task PolicyRequiresAuthenticatedUserWithExactScope(string policy, string scope, bool authenticated, bool allowed)
    {
        using var services = new ServiceCollection().AddLogging().AddBookAuthorization().BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scope", scope)], authenticated ? "Bearer" : null));

        var result = await authorization.AuthorizeAsync(principal, null, policy);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task RepeatedScopeClaimsAreSupported()
    {
        using var services = new ServiceCollection().AddLogging().AddBookAuthorization().BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scope", "unrelated"), new Claim("scope", "books.search")], "Bearer"));

        var result = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, BookAuthorization.Search);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EveryBookActionRequiresItsAssignedScope()
    {
        var actions = typeof(BooksController).GetMethods()
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();
        Assert.Equal(5, actions.Count);
        Assert.Null(typeof(BooksController).GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var action in actions)
        {
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorization = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal(action.Name == nameof(BooksController.Search) ? BookAuthorization.Search : BookAuthorization.Manage,
                authorization.Policy);
        }
    }
}
