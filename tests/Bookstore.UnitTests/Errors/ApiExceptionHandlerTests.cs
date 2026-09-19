using System.Text.Json;
using Bookstore.Api.Errors;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bookstore.UnitTests.Errors;

public sealed class ApiExceptionHandlerTests
{
    [Theory]
    [InlineData("book", 404)]
    [InlineData("author", 400)]
    [InlineData("name", 409)]
    public async Task Expected_failures_return_the_contract_status_and_problem_details(string failure, int status)
    {
        Exception exception = failure switch
        {
            "book" => new BookNotFoundException(),
            "author" => new AuthorNotFoundException(),
            _ => new AuthorNameConflictException()
        };

        using var provider = CreateServices();
        var context = CreateContext(provider, "application/json");
        var handler = new ApiExceptionHandler(
            provider.GetRequiredService<ProblemDetailsFactory>(), new RecordingLogger());

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        if (status == 400)
        {
            Assert.Equal(exception.Message,
                json.RootElement.GetProperty("errors").GetProperty("Author.AuthorId")[0].GetString());
        }
        else
        {
            Assert.Equal(exception.Message, json.RootElement.GetProperty("detail").GetString());
        }
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task Other_accept_headers_still_receive_validation_problem_json(string accept)
    {
        using var provider = CreateServices();
        var context = CreateContext(provider, accept);
        var handler = new ApiExceptionHandler(
            provider.GetRequiredService<ProblemDetailsFactory>(), new RecordingLogger());

        await handler.TryHandleAsync(context, new AuthorNotFoundException(), CancellationToken.None);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Author.AuthorId", out _));
    }

    [Fact]
    public async Task Unexpected_failure_hides_sensitive_exception_details_in_response_and_logs()
    {
        const string privateDetails = "sensitive-connection-details-must-not-escape";
        using var provider = CreateServices();
        var context = CreateContext(provider, "application/json");
        var logger = new RecordingLogger();
        var handler = new ApiExceptionHandler(provider.GetRequiredService<ProblemDetailsFactory>(), logger);

        await handler.TryHandleAsync(context, new InvalidOperationException(privateDetails), CancellationToken.None);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.DoesNotContain(privateDetails, json.RootElement.GetRawText());
        var traceId = json.RootElement.GetProperty("traceId").GetString()!;
        Assert.Contains(logger.Messages, message => message.Contains(traceId, StringComparison.Ordinal));
        Assert.All(logger.Messages, message => Assert.DoesNotContain(privateDetails, message));
        Assert.All(logger.Exceptions, exception => Assert.Null(exception));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddProblemDetails();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string accept)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers.Accept = accept;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class RecordingLogger : ILogger<ApiExceptionHandler>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
