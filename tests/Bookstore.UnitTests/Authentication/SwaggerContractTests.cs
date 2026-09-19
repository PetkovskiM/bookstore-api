using System.Text.Json;
using Bookstore.Api.Controllers;
using Bookstore.Api.OpenApi;
using Bookstore.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookstore.UnitTests.Authentication;

public sealed class SwaggerContractTests
{
    [Fact]
    public async Task SerializedDocumentDescribesTheCorrectTokenForEveryOperation()
    {
        // Generate controller metadata without starting an HTTP server or resolving a database.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BooksController).Assembly.FullName,
            EnvironmentName = "Production"
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(BooksController).Assembly);
        builder.Services.Configure<JwtSettings>(settings => settings.Authority = "https://issuer.example/");
        builder.Services.AddSwaggerGen();
        builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        await using var app = builder.Build();
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        using var json = JsonDocument.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0));
        var root = json.RootElement;
        var operationCount = 0;

        foreach (var path in root.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            operationCount++;
            var requirement = Assert.Single(operation.Value.GetProperty("security").EnumerateArray());
            var scheme = Assert.Single(requirement.EnumerateObject());
            var search = path.Name.EndsWith("/search", StringComparison.Ordinal);
            Assert.Equal(search ? "SearchOAuth" : "ManagementToken", scheme.Name);
            Assert.Equal(search ? ["books.search"] : Array.Empty<string>(),
                scheme.Value.EnumerateArray().Select(scope => scope.GetString()).ToArray());
            foreach (var status in new[] { "401", "403" })
                Assert.True(operation.Value.GetProperty("responses").GetProperty(status)
                    .GetProperty("content").TryGetProperty("application/problem+json", out _));
        }
        Assert.Equal(5, operationCount);
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal("https://issuer.example/connect/authorize", schemes.GetProperty("SearchOAuth")
            .GetProperty("flows").GetProperty("implicit").GetProperty("authorizationUrl").GetString());
        Assert.Equal("bearer", schemes.GetProperty("ManagementToken").GetProperty("scheme").GetString());
    }
}
