using Jobly.Domain.Common;

namespace Jobly.Domain.Tests.Common;

public sealed class ErrorTests
{
    [Fact]
    public void Validation_WithCodeAndMessage_HasValidationType()
    {
        // Act
        var error = Error.Validation("Job.TitleEmpty", "The title is required.");

        // Assert
        Assert.Equal("Job.TitleEmpty", error.Code);
        Assert.Equal("The title is required.", error.Message);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void NotFound_WithCodeAndMessage_HasNotFoundType()
    {
        Assert.Equal(ErrorType.NotFound, Error.NotFound("Code", "Message").Type);
    }

    [Fact]
    public void Conflict_WithCodeAndMessage_HasConflictType()
    {
        Assert.Equal(ErrorType.Conflict, Error.Conflict("Code", "Message").Type);
    }

    [Fact]
    public void Unauthorized_WithCodeAndMessage_HasUnauthorizedType()
    {
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("Code", "Message").Type);
    }

    [Fact]
    public void Forbidden_WithCodeAndMessage_HasForbiddenType()
    {
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("Code", "Message").Type);
    }

    [Fact]
    public void Failure_WithCodeAndMessage_HasFailureType()
    {
        Assert.Equal(ErrorType.Failure, Error.Failure("Code", "Message").Type);
    }

    [Fact]
    public void Equality_SameCodeMessageAndType_AreEqual()
    {
        // Records compare by value, so two errors with the same content are equal (useful in tests and checks).
        Assert.Equal(Error.NotFound("Code", "Message"), Error.NotFound("Code", "Message"));
    }
}
