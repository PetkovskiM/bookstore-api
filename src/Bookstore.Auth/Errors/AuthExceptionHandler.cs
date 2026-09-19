using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bookstore.Auth.Errors;

public sealed class AuthExceptionHandler(ProblemDetailsFactory problems, ILogger<AuthExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = problems.CreateProblemDetails(context, StatusCodes.Status500InternalServerError,
            detail: "The request could not be completed. Please try again later.");
        logger.LogError("Authentication request failed with {ExceptionType}. Trace ID: {TraceId}.",
            exception.GetType().FullName, problem.Extensions["traceId"]);
        await Results.Problem(problem).ExecuteAsync(context);
        return true;
    }
}
