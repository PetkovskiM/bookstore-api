using Bookstore.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookstore.Api.OpenApi;

public sealed class BookSecurityFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            var policy = description.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>()
                .Select(attribute => attribute.Policy)
                .SingleOrDefault(policy => policy is BookAuthorization.Manage or BookAuthorization.Search);
            if (policy is null || description.HttpMethod is null) continue;

            var operation = document.Paths["/" + description.RelativePath!].Operations![new HttpMethod(description.HttpMethod)];
            var search = policy == BookAuthorization.Search;
            // References must belong to the completed document to serialize correctly in OpenAPI.NET.
            // Each operation requires only its own scheme; two entries in one requirement mean AND.
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(search ? ConfigureSwaggerOptions.SearchScheme : ConfigureSwaggerOptions.ManagementScheme, document)] =
                        search ? [BookAuthorization.Search] : []
                }
            ];
        }
    }
}
