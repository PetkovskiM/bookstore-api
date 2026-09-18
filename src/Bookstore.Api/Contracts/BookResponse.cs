using System.Text.Json.Serialization;

namespace Bookstore.Api.Contracts;

public sealed record BookResponse(int BookId, AuthorResponse Author, string Title, [property: JsonPropertyName("subTitle")] string? SubTitle);
