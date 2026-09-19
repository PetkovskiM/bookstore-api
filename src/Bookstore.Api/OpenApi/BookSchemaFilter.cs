using System.Text.Json.Nodes;
using Bookstore.Api.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookstore.Api.OpenApi;

public sealed class BookSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema model, SchemaFilterContext context)
    {
        if (model is not OpenApiSchema schema) return;
        if (context.Type == typeof(BookResponse))
        {
            schema.Required = new HashSet<string> { "bookId", "author", "title" };
            schema.Example = JsonNode.Parse("""
                {"bookId":1,"author":{"authorId":1,"name":"Frank Herbert"},"title":"Dune","subTitle":null}
                """);
            SetGeneratedId(schema, "bookId");
            SetTextLimits(schema, "title");
        }
        else if (context.Type == typeof(AuthorResponse))
        {
            schema.Required = new HashSet<string> { "authorId", "name" };
            SetGeneratedId(schema, "authorId");
            SetTextLimits(schema, "name");
        }
        else if (context.Type == typeof(AuthorRequest))
        {
            schema.AdditionalPropertiesAllowed = true;
            schema.Description = "Omit authorId to create a new author, even if the name already exists. "
                + "A positive authorId reuses an existing author: unknown IDs return 400; names must match after trimming "
                + "(case-sensitive), or return 409. Book writes never rename shared authors or merge by name.";
        }
        else if (context.Type == typeof(CreateBookRequest) || context.Type == typeof(BookWriteRequest))
        {
            schema.AdditionalPropertiesAllowed = true;
            schema.Description = "Title and author name are required and trimmed before validating 3–100 characters. "
                + "subTitle is optional and has no length limit. "
                + (context.Type == typeof(CreateBookRequest)
                    ? "IDs are database-generated; supplying bookId on creation, including null or 0, returns 400."
                    : "Replacement uses the route bookId, never upserts, and clears subTitle when omitted or null. "
                        + "Unmapped fields, including a body bookId on replacement, are ignored.");
            schema.Example = JsonNode.Parse("""
                {"title":"A New Book","author":{"name":"A New Author"},"subTitle":"First edition"}
                """);
        }
        else if (context.Type == typeof(BookSearchResponse))
        {
            schema.Required = new HashSet<string> { "items", "totalCount", "pageNumber", "pageSize" };
            schema.Example = JsonNode.Parse("""
                {"items":[{"bookId":1,"author":{"authorId":1,"name":"Frank Herbert"},"title":"Dune","subTitle":null}],"totalCount":1,"pageNumber":1,"pageSize":10}
                """);
        }
    }

    private static void SetGeneratedId(OpenApiSchema schema, string name)
    {
        if (schema.Properties?[name] is OpenApiSchema property)
        {
            property.Minimum = "1";
            property.Description = "Database-generated integer. Example IDs are illustrative.";
        }
    }

    private static void SetTextLimits(OpenApiSchema schema, string name)
    {
        if (schema.Properties?[name] is OpenApiSchema property)
        {
            property.MinLength = 3;
            property.MaxLength = 100;
        }
    }
}
