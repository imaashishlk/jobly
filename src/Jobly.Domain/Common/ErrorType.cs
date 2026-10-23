namespace Jobly.Domain.Common;

/// <summary>
/// The kind of failure. The API turns each kind into an HTTP status code (see Jobly.Api/Common/ResultExtensions.cs).
/// Adding a value here makes the build fail until every switch over ErrorType handles it.
/// </summary>
public enum ErrorType
{
    /// <summary>Something went wrong that the caller can't fix (maps to 500).</summary>
    Failure,

    /// <summary>The input breaks a rule, for example an empty title (maps to 400).</summary>
    Validation,

    /// <summary>The requested record doesn't exist (maps to 404).</summary>
    NotFound,

    /// <summary>The request clashes with the current state, for example a duplicate (maps to 409).</summary>
    Conflict,

    /// <summary>The caller isn't signed in (maps to 401).</summary>
    Unauthorized,

    /// <summary>The caller is signed in but not allowed, for example not the owner (maps to 403).</summary>
    Forbidden,

    /// <summary>An outside service we depend on is down or too slow, for example the LLM (maps to 503).</summary>
    Unavailable,
}
