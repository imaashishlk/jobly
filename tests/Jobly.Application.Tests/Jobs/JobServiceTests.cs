using Jobly.Application.Jobs;
using Jobly.Application.Tests.Fakes;
using Jobly.Domain.Jobs;

namespace Jobly.Application.Tests.Jobs;

public sealed class JobServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeJobRepository repository = new();
    private readonly JobService service;

    public JobServiceTests()
    {
        service = new JobService(repository, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task CreateJob_ValidRequest_SavesJobInSavedColumn()
    {
        // Arrange
        var request = new CreateJobRequest { Title = "Backend Developer", Company = "Acme", Description = "Build APIs." };

        // Act
        var result = await service.CreateJob(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(repository.Jobs);
        Assert.Equal(ApplicationStatus.Saved, result.Value.Status);
        Assert.Equal(JobSource.Manual, result.Value.Source);
        Assert.Equal(Now, result.Value.CreatedAt);
    }

    [Fact]
    public async Task CreateJob_TitleIsEmpty_ReturnsValidationErrorAndSavesNothing()
    {
        // Arrange
        var request = new CreateJobRequest { Title = " ", Company = "Acme", Description = "Build APIs." };

        // Act
        var result = await service.CreateJob(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.TitleRequired", result.Error.Code);
        Assert.Empty(repository.Jobs);
    }

    [Fact]
    public async Task GetJob_JobExists_ReturnsJob()
    {
        // Arrange
        var job = await AddJob("Developer", Now);

        // Act
        var result = await service.GetJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Developer", result.Value.Title);
    }

    [Fact]
    public async Task GetJob_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await service.GetJob(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task ListJobs_NoFilter_ReturnsAllJobsNewestFirst()
    {
        // Arrange
        await AddJob("Older", Now.AddDays(-1));
        await AddJob("Newer", Now);

        // Act
        var jobs = await service.ListJobs(null, CancellationToken.None);

        // Assert
        Assert.Equal(["Newer", "Older"], jobs.Select(job => job.Title));
    }

    [Fact]
    public async Task ListJobs_WithStatus_ReturnsOnlyJobsInThatColumn()
    {
        // Arrange
        await AddJob("Still saved", Now);
        var applied = await AddJob("Applied one", Now);
        applied.Application.ChangeStatus(ApplicationStatus.Applied, Now);

        // Act
        var jobs = await service.ListJobs(ApplicationStatus.Applied, CancellationToken.None);

        // Assert
        Assert.Equal(["Applied one"], jobs.Select(job => job.Title));
    }

    [Fact]
    public async Task UpdateJob_ValidRequest_ChangesJob()
    {
        // Arrange
        var job = await AddJob("Developer", Now);
        var request = new UpdateJobRequest { Title = "Senior Developer", Company = "Acme AS", Description = "New text" };

        // Act
        var result = await service.UpdateJob(job.Id, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Senior Developer", result.Value.Title);
        Assert.Equal("Senior Developer", job.Title);
    }

    [Fact]
    public async Task UpdateJob_TitleIsEmpty_ReturnsValidationErrorAndKeepsJob()
    {
        // Arrange
        var job = await AddJob("Developer", Now);
        var request = new UpdateJobRequest { Title = "", Company = "Acme", Description = "Description" };

        // Act
        var result = await service.UpdateJob(job.Id, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.TitleRequired", result.Error.Code);
        Assert.Equal("Developer", job.Title);
    }

    [Fact]
    public async Task UpdateJob_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateJobRequest { Title = "Developer", Company = "Acme", Description = "Description" };

        // Act
        var result = await service.UpdateJob(Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        Assert.Equal("Job.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task ChangeStatus_AllowedMove_MovesJobAndReturnsNewStatus()
    {
        // Arrange
        var job = await AddJob("Developer", Now);

        // Act
        var result = await service.ChangeStatus(job.Id, new ChangeStatusRequest { Status = ApplicationStatus.Applied }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationStatus.Applied, result.Value.Status);
        Assert.Equal(Now, result.Value.AppliedAt);
    }

    [Fact]
    public async Task ChangeStatus_DisallowedMove_ReturnsConflictAndKeepsStatus()
    {
        // Arrange: a job that was just saved can't jump straight to Offer.
        var job = await AddJob("Developer", Now);

        // Act
        var result = await service.ChangeStatus(job.Id, new ChangeStatusRequest { Status = ApplicationStatus.Offer }, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.InvalidStatusChange", result.Error.Code);
        Assert.Equal(ApplicationStatus.Saved, job.Application.Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await service.ChangeStatus(Guid.NewGuid(), new ChangeStatusRequest { Status = ApplicationStatus.Applied }, CancellationToken.None);

        // Assert
        Assert.Equal("Job.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateNotes_JobExists_SavesNotes()
    {
        // Arrange
        var job = await AddJob("Developer", Now);

        // Act
        var result = await service.UpdateNotes(job.Id, new UpdateNotesRequest { Notes = "Call on Monday" }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Call on Monday", result.Value.Notes);
    }

    [Fact]
    public async Task UpdateNotes_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await service.UpdateNotes(Guid.NewGuid(), new UpdateNotesRequest { Notes = "Note" }, CancellationToken.None);

        // Assert
        Assert.Equal("Job.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DeleteJob_JobExists_RemovesIt()
    {
        // Arrange
        var job = await AddJob("Developer", Now);

        // Act
        var result = await service.DeleteJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Jobs);
    }

    [Fact]
    public async Task DeleteJob_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await service.DeleteJob(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Equal("Job.NotFound", result.Error.Code);
    }

    // Puts a job straight into the fake repository, bypassing the service, so each test controls its own starting data.
    private async Task<Job> AddJob(string title, DateTimeOffset createdAt)
    {
        var job = Job.Create(title, "Acme", null, "Description", null, JobSource.Manual, null, createdAt).Value;
        await repository.Add(job, CancellationToken.None);
        return job;
    }
}
