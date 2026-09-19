using System.Text.Json;
using Bookstore.Auth.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bookstore.UnitTests.Authentication;

public sealed class AuthExceptionHandlerTests
{
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    public async Task Unexpected_auth_errors_keep_private_details_out_of_responses_and_logs(string accept)
    {
        const string privateDetails = "private-authentication-details-must-not-escape";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddProblemDetails();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "auth-error-test" };
        context.Request.Headers.Accept = accept;
        context.Response.Body = new MemoryStream();
        var logger = new RecordingLogger();
        var handler = new AuthExceptionHandler(provider.GetRequiredService<ProblemDetailsFactory>(), logger);

        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException(privateDetails), CancellationToken.None));

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.DoesNotContain(privateDetails, json.RootElement.GetRawText());
        Assert.Contains("auth-error-test", Assert.Single(logger.Messages));
        Assert.DoesNotContain(privateDetails, logger.Messages[0]);
        Assert.Null(Assert.Single(logger.Exceptions));
    }

    private sealed class RecordingLogger : ILogger<AuthExceptionHandler>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
