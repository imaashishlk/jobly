using Jobly.Domain.Jobs;

namespace Jobly.Domain.Tests.Jobs;

public sealed class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInput_ReturnsJobWithSavedApplication()
    {
        // Act
        var result = Job.Create("  Backend Developer ", " Acme AS ", "Stavanger", "Build APIs.", "https://acme.no/job", JobSource.Manual, null, Now);

        // Assert: text is trimmed, and a new job always starts in the Saved column.
        Assert.True(result.IsSuccess);
        var job = result.Value;
        Assert.Equal("Backend Developer", job.Title);
        Assert.Equal("Acme AS", job.Company);
        Assert.Equal(Now, job.CreatedAt);
        Assert.Equal(ApplicationStatus.Saved, job.Application.Status);
        Assert.Equal(job.Id, job.Application.JobId);
        Assert.Null(job.Application.AppliedAt);
    }

    [Theory]
    [InlineData("", "Acme", "Description", "Job.TitleRequired")]
    [InlineData("   ", "Acme", "Description", "Job.TitleRequired")]
    [InlineData("Developer", "", "Description", "Job.CompanyRequired")]
    [InlineData("Developer", "Acme", " ", "Job.DescriptionRequired")]
    public void Create_RequiredFieldIsEmpty_ReturnsValidationError(string title, string company, string description, string expectedCode)
    {
        // Act
        var result = Job.Create(title, company, null, description, null, JobSource.Manual, null, Now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public void Update_ValidInput_ChangesFieldsButKeepsSourceAndStatus()
    {
        // Arrange
        var job = Job.Create("Developer", "Acme", null, "Old text", null, JobSource.Arbeidsplassen, "ad-42", Now).Value;

        // Act
        var result = job.Update(" Senior Developer ", "Acme AS", "Oslo", "New text", "https://acme.no");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Senior Developer", job.Title);
        Assert.Equal("Oslo", job.Location);
        Assert.Equal("New text", job.Description);
        Assert.Equal(JobSource.Arbeidsplassen, job.Source);
        Assert.Equal("ad-42", job.ExternalId);
        Assert.Equal(ApplicationStatus.Saved, job.Application.Status);
    }

    [Theory]
    [InlineData("", "Acme", "Description", "Job.TitleRequired")]
    [InlineData("Developer", " ", "Description", "Job.CompanyRequired")]
    [InlineData("Developer", "Acme", "", "Job.DescriptionRequired")]
    public void Update_RequiredFieldIsEmpty_ReturnsValidationErrorAndKeepsOldValues(string title, string company, string description, string expectedCode)
    {
        // Arrange
        var job = Job.Create("Developer", "Acme", null, "Old text", null, JobSource.Manual, null, Now).Value;

        // Act
        var result = job.Update(title, company, null, description, null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal("Developer", job.Title);
        Assert.Equal("Old text", job.Description);
    }
}
