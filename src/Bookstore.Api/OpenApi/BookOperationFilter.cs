using System.Text.Json.Nodes;
using Bookstore.Api.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookstore.Api.OpenApi;

public sealed class BookOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(BooksController)) return;

        var search = context.MethodInfo.Name == nameof(BooksController.Search);
        (operation.Summary, operation.Description) = context.MethodInfo.Name switch
        {
            nameof(BooksController.Search) => ("Search books", "Requires books.search from the implicit flow. "
                + "Title and author name use case-insensitive, literal substring matching with AND. "
                + "Trimmed blank filters are absent; no filters returns all books, paginated. "
                + "Results are ordered by bookId; totalCount is before pagination. No matches or pages beyond the end return 200 with empty items."),
            nameof(BooksController.GetById) => ("Get a book", "Requires books.manage from client credentials. Missing books return 404."),
            nameof(BooksController.Create) => ("Create a book", "Requires books.manage from client credentials. "
                + "Returns 201, the generated IDs and a Location header. Explicit bookId is rejected. "
                + "Omit authorId for a new author, or use an existing positive ID and its exact trimmed name."),
            nameof(BooksController.Replace) => ("Replace a book", "Requires books.manage from client credentials. "
                + "Uses the route bookId; returns 404 rather than creating a missing book. Replaces editable fields. "
                + "Omitting subTitle or sending null clears it. Author creation/reuse follows the same rules as POST."),
            nameof(BooksController.Delete) => ("Delete a book", "Requires books.manage from client credentials. Returns 204 with no body. The author remains; missing books return 404."),
            _ => (operation.Summary, operation.Description)
        };
        if (operation.Responses is { } responses)
        {
            foreach (var (status, value) in responses)
            {
                if (value is OpenApiResponse result && result.Content?.Values.FirstOrDefault() is { } mediaType)
                {
                    var contentType = int.TryParse(status, out var code) && code >= 400
                        ? "application/problem+json" : "application/json";
                    result.Content = new Dictionary<string, OpenApiMediaType> { [contentType] = mediaType };
                }
            }
            responses["401"].Description = "Missing or invalid bearer token (issuer, audience, signature, type or expiry). Includes WWW-Authenticate: Bearer.";
            responses["403"].Description = "Valid token without this operation's required scope.";
            if (responses.TryGetValue("201", out var created) && created is OpenApiResponse response)
            {
                response.Headers = new Dictionary<string, IOpenApiHeader>
                {
                    ["Location"] = new OpenApiHeader { Description = "GET URL of the created book", Schema = new OpenApiSchema { Type = JsonSchemaType.String } }
                };
            }
        }
        if (operation.RequestBody?.Content is { } content)
        {
            foreach (var mediaType in content.Values)
            {
                mediaType.Examples = new Dictionary<string, IOpenApiExample>
                {
                    ["newAuthor"] = new OpenApiExample
                    {
                        Summary = "Create a new author",
                        Value = JsonNode.Parse("""{"title":"A New Book","author":{"name":"A New Author"},"subTitle":"First edition"}""")
                    },
                    ["existingAuthor"] = new OpenApiExample
                    {
                        Summary = "Reuse an author (replace with an actual returned ID/name)",
                        Value = JsonNode.Parse("""{"title":"Another Book","author":{"authorId":1,"name":"Frank Herbert"},"subTitle":null}""")
                    }
                };
            }
        }
        if (!search || operation.Parameters is null) return;
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            parameter.Description = parameter.Name switch
            {
                "title" => "Optional title substring; trimmed, case-insensitive, blank means absent.",
                "author" => "Optional author name substring; combined with title using AND.",
                "pageNumber" => "Positive page number; default 1. A page beyond the end is empty.",
                "pageSize" => "Page size from 1 to 100; default 10. Invalid pagination returns 400.",
                _ => parameter.Description
            };
            if (parameter.Schema is OpenApiSchema schema && parameter.Name is "pageNumber" or "pageSize")
                schema.Default = JsonValue.Create(parameter.Name == "pageNumber" ? 1 : 10);
        }
    }
}
