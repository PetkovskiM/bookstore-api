using System.Text.Json;
using Bookstore.Api.Contracts;

namespace Bookstore.UnitTests.Contracts;

public sealed class JsonContractTests
{
    [Fact]
    public void Book_response_contains_both_ids_nested_author_and_exact_subTitle_spelling()
    {
        var response = new BookResponse(7, new AuthorResponse(3, "Frank Herbert"), "Dune", "An edition");

        var json = JsonSerializer.SerializeToElement(response, JsonSerializerOptions.Web);

        Assert.Equal(new[] { "author", "bookId", "subTitle", "title" },
            json.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(7, json.GetProperty("bookId").GetInt32());
        Assert.Equal(3, json.GetProperty("author").GetProperty("authorId").GetInt32());
        Assert.Equal("Frank Herbert", json.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("An edition", json.GetProperty("subTitle").GetString());
    }

    [Fact]
    public void Write_payloads_omit_book_id_and_keep_subTitle_spelling()
    {
        var replacement = new BookWriteRequest
        {
            Title = "Dune",
            Author = new AuthorRequest { AuthorId = 3, Name = "Frank Herbert" },
            SubTitle = "An edition"
        };
        var creation = new CreateBookRequest
        {
            Title = replacement.Title,
            Author = replacement.Author,
            SubTitle = replacement.SubTitle
        };

        var replacementJson = JsonSerializer.SerializeToElement(replacement, JsonSerializerOptions.Web);
        var creationJson = JsonSerializer.SerializeToElement(creation, JsonSerializerOptions.Web);

        Assert.False(replacementJson.TryGetProperty("bookId", out _));
        Assert.False(creationJson.TryGetProperty("bookId", out _));
        Assert.False(creationJson.TryGetProperty("additionalProperties", out _));
        Assert.Equal("An edition", replacementJson.GetProperty("subTitle").GetString());
        Assert.Equal("An edition", creationJson.GetProperty("subTitle").GetString());
    }

    [Theory]
    [InlineData("""{ "title": "Dune", "author": { "name": "Frank Herbert" } }""")]
    [InlineData("""{ "title": "Dune", "author": { "name": "Frank Herbert" }, "subTitle": null }""")]
    public void Replacement_with_missing_or_null_subtitle_deserializes_to_null(string json)
    {
        var request = JsonSerializer.Deserialize<BookWriteRequest>(json, JsonSerializerOptions.Web)!;

        Assert.Null(request.SubTitle);
        Assert.True(RequestValidation.Validate(request).IsValid);
    }

    [Fact]
    public void Empty_search_response_serializes_items_as_an_array_and_preserves_page_metadata()
    {
        var response = new BookSearchResponse([], TotalCount: 21, PageNumber: 4, PageSize: 10);

        var json = JsonSerializer.SerializeToElement(response, JsonSerializerOptions.Web);

        Assert.Equal(JsonValueKind.Array, json.GetProperty("items").ValueKind);
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
        Assert.Equal(21, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, json.GetProperty("pageNumber").GetInt32());
        Assert.Equal(10, json.GetProperty("pageSize").GetInt32());
    }
}
