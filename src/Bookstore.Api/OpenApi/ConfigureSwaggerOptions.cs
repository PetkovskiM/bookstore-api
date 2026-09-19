using Bookstore.Api.Security;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookstore.Api.OpenApi;

public sealed class ConfigureSwaggerOptions(IOptions<JwtSettings> settings) : IConfigureOptions<SwaggerGenOptions>
{
    public const string ManagementScheme = "ManagementToken";
    public const string SearchScheme = "SearchOAuth";

    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Bookstore API",
            Version = "v1",
            Description = "CRUD requires a client-credentials token with books.manage. "
                + "Search requires an implicit-flow token with books.search. Use Authorize to configure each separately. "
                + "The examples document our contract assumptions; sample IDs are illustrative, not fixed seed IDs."
        });
        options.SupportNonNullableReferenceTypes();
        options.DescribeAllParametersInCamelCase();
        options.AddSecurityDefinition(ManagementScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste only the access_token obtained with client_credentials and books.manage. "
                + "See docs/api-security-swagger.md for the PowerShell example. Keep the client secret out of Swagger."
        });
        options.AddSecurityDefinition(SearchScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Public bookstore-browser client: sign in as the development demo user. "
                + "No client secret. Implicit is required by the assignment; production clients should use Authorization Code with PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                Implicit = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(new Uri(settings.Value.Authority), "connect/authorize"),
                    Scopes = new Dictionary<string, string> { [BookAuthorization.Search] = "Search books with pagination" }
                }
            }
        });
        options.SchemaFilter<BookSchemaFilter>();
        options.OperationFilter<BookOperationFilter>();
        options.DocumentFilter<BookSecurityFilter>();
    }
}
