using Bookstore.Api.Contracts;
using Bookstore.Api.Models;

namespace Bookstore.Api.Services;

public static class AuthorRules
{
    // Shared by creation and replacement; MVC validates the request fields first.
    public static Author Resolve(AuthorRequest request, Author? referencedAuthor)
    {
        if (request.AuthorId is null)
        {
            return new Author { Name = request.Name! };
        }

        if (referencedAuthor is null || referencedAuthor.AuthorId != request.AuthorId)
        {
            throw new AuthorNotFoundException();
        }

        if (!string.Equals(request.Name, referencedAuthor.Name.Trim(), StringComparison.Ordinal))
        {
            throw new AuthorNameConflictException();
        }

        return referencedAuthor;
    }
}
