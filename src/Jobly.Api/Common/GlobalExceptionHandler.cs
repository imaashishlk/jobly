using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Jobly.Api.Common;

/// <summary>
/// The last line of defense: catches any exception nobody else handled, logs it once,
/// and returns a clean 500 ProblemDetails response. Expected failures never get here;
/// they travel as Result errors (see .claude/rules/code-style.md).
/// </summary>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Logged as Error with the full exception (stack trace included), plus method and path as searchable fields.
        // The request's trace ID is attached automatically, so the traceId in the response leads straight to this log.
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // The response says only that something went wrong. The exception message can contain internal
        // details (SQL, file paths, keys), so it stays in the log and never goes to the caller.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "Something went wrong on our side. Use the traceId when reporting this problem.",
            },
        });
    }
}
