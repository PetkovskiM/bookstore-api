using Bookstore.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bookstore.Api.Errors;

public sealed class ApiExceptionHandler(ProblemDetailsFactory problemDetailsFactory, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case BookNotFoundException:
                problem = problemDetailsFactory.CreateProblemDetails(httpContext, StatusCodes.Status404NotFound, detail: exception.Message);
                break;
            case AuthorNotFoundException:
                var errors = new ModelStateDictionary();
                errors.AddModelError("Author.AuthorId", exception.Message);
                problem = problemDetailsFactory.CreateValidationProblemDetails(httpContext, errors);
                break;
            case AuthorNameConflictException:
                problem = problemDetailsFactory.CreateProblemDetails(httpContext, StatusCodes.Status409Conflict, detail: exception.Message);
                break;
            default:
                problem = problemDetailsFactory.CreateProblemDetails(httpContext, StatusCodes.Status500InternalServerError,
                    detail: "The request could not be completed. Please try again later.");
                // Raw exceptions can contain connection strings or other credentials.
                logger.LogError("Unhandled exception of type {ExceptionType}. Trace ID: {TraceId}.",
                    exception.GetType().FullName, problem.Extensions["traceId"]);
                break;
        }

        // Uses AddProblemDetails, with a JSON fallback for other Accept headers.
        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
