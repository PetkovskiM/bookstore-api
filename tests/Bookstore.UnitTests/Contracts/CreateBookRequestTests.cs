using System.Text.Json;
using Bookstore.Api.Contracts;

namespace Bookstore.UnitTests.Contracts;

public sealed class CreateBookRequestTests
{
    [Theory]
    [InlineData("bookId", "42")]
    [InlineData("bookId", "0")]
    [InlineData("bookId", "null")]
    [InlineData("BookId", "42")]
    [InlineData("BOOKID", "42")]
    public void Explicit_book_id_is_rejected_regardless_of_value_or_casing(string propertyName, string value)
    {
        var json = $$"""
            { "{{propertyName}}": {{value}}, "title": "Dune", "author": { "name": "Frank Herbert" } }
            """;
        var request = JsonSerializer.Deserialize<CreateBookRequest>(json, JsonSerializerOptions.Web)!;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains("bookId", errors.Keys);
    }

    [Fact]
    public void Deserialized_new_book_without_ids_is_valid_and_normalized()
    {
        const string json = """
            { "title": "  Dune  ", "author": { "name": "  Frank Herbert  " }, "subTitle": "  An edition  " }
            """;

        var request = JsonSerializer.Deserialize<CreateBookRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(RequestValidation.Validate(request).IsValid);
        Assert.Equal("Dune", request.Title);
        Assert.Equal("Frank Herbert", request.Author!.Name);
        Assert.Null(request.Author.AuthorId);
        Assert.Equal("  An edition  ", request.SubTitle);
    }

    [Fact]
    public void Creation_still_requires_the_shared_write_fields()
    {
        var request = JsonSerializer.Deserialize<CreateBookRequest>("{}", JsonSerializerOptions.Web)!;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains(nameof(BookWriteRequest.Title), errors.Keys);
        Assert.Contains(nameof(BookWriteRequest.Author), errors.Keys);
    }

    [Fact]
    public void Creation_validates_the_nested_author()
    {
        const string json = """
            { "title": "Dune", "author": { "name": "  ab  " } }
            """;
        var request = JsonSerializer.Deserialize<CreateBookRequest>(json, JsonSerializerOptions.Web)!;

        var errors = RequestValidation.Validate(request);

        Assert.False(errors.IsValid);
        Assert.Contains("Author.Name", errors.Keys);
    }

    [Fact]
    public void Unrelated_unknown_json_properties_do_not_introduce_extra_validation_rules()
    {
        const string json = """
            { "title": "Dune", "author": { "name": "Frank Herbert" }, "clientNote": "demo" }
            """;
        var request = JsonSerializer.Deserialize<CreateBookRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(RequestValidation.Validate(request).IsValid);
    }
}
