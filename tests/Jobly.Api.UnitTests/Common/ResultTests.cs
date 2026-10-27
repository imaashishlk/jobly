using Jobly.Api.Common;

namespace Jobly.Api.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.NotFound("Job.NotFound", "The job was not found.");

    [Fact]
    public void Success_WithoutValue_IsSuccess()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Failure_WithError_IsFailureAndHoldsError()
    {
        // Act
        var result = Result.Failure(SampleError);

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(SampleError, result.Error);
    }

    [Fact]
    public void Error_OfSuccessfulResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Success();

        // Act and Assert: a success has no error, so asking for one is a programming mistake.
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Success_WithValue_IsSuccessAndHoldsValue()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_OfFailedResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Failure<int>(SampleError);

        // Act and Assert: a failure has no value, so callers must check IsSuccess first.
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        // Act: returning a plain value from a method that returns Result<int> works without Result.Success(...).
        Result<int> result = 42;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        // Act: returning an Error from a method that returns Result<int> works without Result.Failure(...).
        Result<int> result = SampleError;

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
    }

    [Fact]
    public void ImplicitConversion_FromErrorToNonGenericResult_CreatesFailure()
    {
        // Act
        Result result = SampleError;

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(SampleError, result.Error);
    }
}
