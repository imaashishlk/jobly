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
        // ASP.NET Core throws this (in Development) when it can't read the request, for example ?status=Banana.
        // That is the caller's mistake, not a bug of ours, so it becomes a 400 and not a 500.
        if (exception is BadHttpRequestException)
        {
            // Warning, not Error: nothing is broken on our side. Shows how often callers send unreadable requests.
            logger.LogWarning("Unreadable request for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            return await WriteProblem(
                httpContext,
                exception,
                StatusCodes.Status400BadRequest,
                "The request could not be read.",
                "Check the values you sent, for example a status that does not exist.");
        }

        // Logged as Error with the full exception (stack trace included), plus method and path as searchable fields.
        // The request's trace ID is attached automatically, so the traceId in the response leads straight to this log.
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        // The response says only that something went wrong. The exception message can contain internal
        // details (SQL, file paths, keys), so it stays in the log and never goes to the caller.
        return await WriteProblem(
            httpContext,
            exception,
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred.",
            "Something went wrong on our side. Use the traceId when reporting this problem.");
    }

    private async ValueTask<bool> WriteProblem(HttpContext httpContext, Exception exception, int statusCode, string title, string detail)
    {
        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = statusCode, Title = title, Detail = detail },
        });
    }
}
