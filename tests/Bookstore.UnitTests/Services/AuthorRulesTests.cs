using Bookstore.Api.Contracts;
using Bookstore.Api.Models;
using Bookstore.Api.Services;

namespace Bookstore.UnitTests.Services;

public sealed class AuthorRulesTests
{
    [Fact]
    public void Omitting_the_id_creates_a_new_author_even_when_a_name_matches()
    {
        var existing = new Author { AuthorId = 7, Name = "Frank Herbert" };
        var request = new AuthorRequest { Name = "Frank Herbert" };

        var author = AuthorRules.Resolve(request, existing);

        Assert.NotSame(existing, author);
        Assert.Equal(0, author.AuthorId);
        Assert.Equal("Frank Herbert", author.Name);
        Assert.Equal(7, existing.AuthorId);
    }

    [Fact]
    public void Matching_id_and_trimmed_name_reuses_the_author_without_renaming_it()
    {
        var existing = new Author { AuthorId = 7, Name = " Frank Herbert " };
        var request = new AuthorRequest { AuthorId = 7, Name = "  Frank Herbert  " };

        var author = AuthorRules.Resolve(request, existing);

        Assert.Same(existing, author);
        Assert.Equal(" Frank Herbert ", existing.Name);
    }

    [Fact]
    public void An_unknown_reference_is_rejected_instead_of_creating_an_author()
    {
        var request = new AuthorRequest { AuthorId = 7, Name = "Frank Herbert" };

        Assert.Throws<AuthorNotFoundException>(() => AuthorRules.Resolve(request, null));
    }

    [Fact]
    public void An_author_with_another_id_cannot_satisfy_a_reference_by_name()
    {
        var request = new AuthorRequest { AuthorId = 7, Name = "Frank Herbert" };
        var anotherAuthor = new Author { AuthorId = 8, Name = "Frank Herbert" };

        Assert.Throws<AuthorNotFoundException>(() => AuthorRules.Resolve(request, anotherAuthor));
    }

    [Theory]
    [InlineData("Isaac Asimov")]
    [InlineData("frank herbert")]
    [InlineData("Frank  Herbert")]
    public void A_different_name_conflicts_without_modifying_the_shared_author(string suppliedName)
    {
        var existing = new Author { AuthorId = 7, Name = "Frank Herbert" };
        var request = new AuthorRequest { AuthorId = 7, Name = suppliedName };

        Assert.Throws<AuthorNameConflictException>(() => AuthorRules.Resolve(request, existing));
        Assert.Equal("Frank Herbert", existing.Name);
    }
}
