using Jobly.Api.Common;
using Jobly.Domain.Common;

namespace Jobly.Api.Tests.Common;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 500)]
    [InlineData(ErrorType.Unavailable, 503)]
    public void ToProblem_ErrorType_ReturnsMatchingStatusCode(ErrorType errorType, int expectedStatusCode)
    {
        // Arrange
        var error = new Error("Test.Code", "Test message.", errorType);

        // Act
        var problem = error.ToProblem();

        // Assert: both the HTTP status and the status inside the ProblemDetails body match.
        Assert.Equal(expectedStatusCode, problem.StatusCode);
        Assert.Equal(expectedStatusCode, problem.ProblemDetails.Status);
    }

    [Fact]
    public void ToProblem_Error_IncludesMessageAndErrorCode()
    {
        // Arrange
        var error = Error.NotFound("Job.NotFound", "The job was not found.");

        // Act
        var problem = error.ToProblem();

        // Assert: the message is the human-readable detail, and the code is there for client code to check.
        Assert.Equal("The job was not found.", problem.ProblemDetails.Detail);
        Assert.Equal("Job.NotFound", problem.ProblemDetails.Extensions["errorCode"]);
    }
}
