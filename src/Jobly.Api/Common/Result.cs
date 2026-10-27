namespace Jobly.Api.Common;

/// <summary>
/// The outcome of an operation that can fail in an expected way: either a success, or a failure carrying an <see cref="Common.Error"/>.
/// Used instead of throwing exceptions for normal outcomes like "not found" (see .claude/rules/code-style.md).
/// </summary>
/// <remarks>Not sealed because <see cref="Result{TValue}"/> extends it to carry a value.</remarks>
public class Result
{
    private readonly Error? error;

    protected Result(bool isSuccess, Error? error)
    {
        // Guard clauses: these combinations can only come from a programming mistake, so throwing is right here.
        if (isSuccess && error is not null) throw new ArgumentException("A successful result cannot have an error.", nameof(error));
        if (!isSuccess && error is null) throw new ArgumentNullException(nameof(error), "A failed result must have an error.");

        IsSuccess = isSuccess;
        this.error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The reason for the failure. Reading it on a success throws, so check <see cref="IsFailure"/> first.</summary>
    public Error Error => error ?? throw new InvalidOperationException("A successful result has no error. Check IsFailure first.");

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value);

    public static Result<TValue> Failure<TValue>(Error error) => new(error);

    // Lets a method that returns Result simply write `return JobErrors.NotFound;`.
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// A <see cref="Result"/> that carries a value when it succeeds.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? value;

    internal Result(TValue value)
        : base(true, null)
    {
        this.value = value;
    }

    internal Result(Error error)
        : base(false, error)
    {
        value = default;
    }

    /// <summary>The value of a success. Reading it on a failure throws, so check <see cref="Result.IsSuccess"/> first.</summary>
    public TValue Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value. Check IsSuccess first.");

    // Lets a method that returns Result<Job> simply write `return job;` on success...
    public static implicit operator Result<TValue>(TValue value) => new(value);

    // ...and `return JobErrors.NotFound;` on failure.
    public static implicit operator Result<TValue>(Error error) => new(error);
}
