namespace Jobly.Domain.Common;

/// <summary>
/// Describes an expected failure, such as "job not found", returned inside a <see cref="Result"/> instead of thrown.
/// </summary>
/// <param name="Code">A stable, searchable identifier such as "Job.NotFound".</param>
/// <param name="Message">A human-readable explanation, safe to show to the caller.</param>
/// <param name="Type">The kind of failure, which decides the HTTP status code.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    // Factory methods keep call sites short and readable: Error.NotFound("Job.NotFound", "The job was not found.").
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}
