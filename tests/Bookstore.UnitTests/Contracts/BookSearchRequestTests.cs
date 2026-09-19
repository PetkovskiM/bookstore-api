using Bookstore.Api.Contracts;

namespace Bookstore.UnitTests.Contracts;

public sealed class BookSearchRequestTests
{
    [Fact]
    public void Omitted_search_parameters_use_valid_defaults()
    {
        var request = new BookSearchRequest();

        Assert.Equal(1, request.PageNumber);
        Assert.Equal(10, request.PageSize);
        Assert.Null(request.Title);
        Assert.Null(request.Author);
        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Blank_search_filters_are_treated_as_absent(string? filter)
    {
        var request = new BookSearchRequest { Title = filter, Author = filter };

        Assert.Null(request.Title);
        Assert.Null(request.Author);
        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Fact]
    public void Search_filters_are_trimmed_independently_without_changing_case()
    {
        var request = new BookSearchRequest { Title = "  DUNE \t", Author = "  Herbert  " };

        Assert.Equal("DUNE", request.Title);
        Assert.Equal("Herbert", request.Author);
    }

    [Fact]
    public void Search_filters_do_not_have_the_book_write_minimum_length()
    {
        var request = new BookSearchRequest { Title = "a", Author = "b" };

        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(0, 10, "PageNumber")]
    [InlineData(-1, 10, "PageNumber")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, -1, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public void Invalid_pagination_is_rejected(int pageNumber, int pageSize, string expectedField)
    {
        var request = new BookSearchRequest { PageNumber = pageNumber, PageSize = pageSize };

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains(expectedField, errors.Keys);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Valid_pagination_boundaries_are_accepted(int pageNumber, int pageSize)
    {
        var request = new BookSearchRequest { PageNumber = pageNumber, PageSize = pageSize };

        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(1, 10, 0L)]
    [InlineData(2, 10, 10L)]
    [InlineData(3, 25, 50L)]
    [InlineData(21_474_838, 100, 2_147_483_700L)]
    [InlineData(int.MaxValue, 100, 214_748_364_600L)]
    public void Page_offsets_do_not_overflow_for_large_valid_page_numbers(int pageNumber, int pageSize, long expectedOffset)
    {
        var request = new BookSearchRequest { PageNumber = pageNumber, PageSize = pageSize };

        Assert.Equal(expectedOffset, request.GetOffset());
    }
}
