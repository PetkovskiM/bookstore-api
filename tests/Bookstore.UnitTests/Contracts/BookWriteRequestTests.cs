using Bookstore.Api.Contracts;

namespace Bookstore.UnitTests.Contracts;

public sealed class BookWriteRequestTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Missing_or_blank_title_is_invalid(string? title)
    {
        var request = ValidRequest();
        request.Title = title;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains(nameof(BookWriteRequest.Title), errors.Keys);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Title_length_is_checked_after_trimming(int length, bool expectedValid)
    {
        var request = ValidRequest();
        request.Title = $"  {new string('a', length)} \t";

        var errors = RequestValidation.Validate(request);

        Assert.Equal(new string('a', length), request.Title);
        Assert.Equal(expectedValid, errors.IsValid);
    }

    [Fact]
    public void Missing_nested_author_is_invalid()
    {
        var request = ValidRequest();
        request.Author = null;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains(nameof(BookWriteRequest.Author), errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Missing_or_blank_nested_author_name_is_invalid(string? name)
    {
        var request = ValidRequest();
        request.Author!.Name = name;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains("Author.Name", errors.Keys);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Nested_author_name_length_is_checked_after_trimming(int length, bool expectedValid)
    {
        var request = ValidRequest();
        request.Author!.Name = $"  {new string('a', length)} \t";

        var errors = RequestValidation.Validate(request);

        Assert.Equal(new string('a', length), request.Author.Name);
        Assert.Equal(expectedValid, errors.IsValid);
        if (!expectedValid)
        {
            Assert.Contains("Author.Name", errors.Keys);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_author_id_is_invalid(int authorId)
    {
        var request = ValidRequest();
        request.Author!.AuthorId = authorId;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains("Author.AuthorId", errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Author_id_can_be_absent_or_a_positive_int32(int? authorId)
    {
        var request = ValidRequest();
        request.Author!.AuthorId = authorId;

        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("  A revised edition \t")]
    public void Subtitle_is_optional_and_is_preserved_without_trimming(string? subTitle)
    {
        var request = ValidRequest();
        request.SubTitle = subTitle;

        Assert.True(RequestValidation.Validate(request).IsValid);
        Assert.Equal(subTitle, request.SubTitle);
    }

    [Fact]
    public void Subtitle_does_not_inherit_the_title_length_limit()
    {
        var request = ValidRequest();
        request.SubTitle = new string('a', 10_000);

        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    private static BookWriteRequest ValidRequest()
    {
        return new BookWriteRequest
        {
            Title = "Dune",
            Author = new AuthorRequest { Name = "Frank Herbert" }
        };
    }
}
