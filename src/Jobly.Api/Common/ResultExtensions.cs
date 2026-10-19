using Jobly.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobly.Api.Common;

/// <summary>
/// The one place where a failed <see cref="Result"/> becomes an HTTP response.
/// Every endpoint uses this, so "not found" is always 404, "forbidden" always 403, and so on.
/// </summary>
internal static class ResultExtensions
{
    /// <summary>
    /// Turns an <see cref="Error"/> into a ProblemDetails response (the standard JSON error format, RFC 9457).
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error)
    {
        // No `_` fallback on purpose: if someone adds a new ErrorType, the build fails here until it's handled.
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
        };

        // "detail" is the human-readable message. "errorCode" lets client code react to a specific error
        // (for example show a "job was removed" screen) without parsing text. The title is filled in
        // automatically from the status code ("Not Found", "Forbidden", ...).
        return TypedResults.Problem(
            detail: error.Message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
    }
}
