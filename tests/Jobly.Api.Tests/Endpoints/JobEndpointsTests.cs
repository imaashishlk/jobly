using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobly.Api.Features.Jobs;

namespace Jobly.Api.Tests.Endpoints;

// A new in-memory API per test (xUnit creates this class once per test), so tests can't see each other's jobs.
public sealed class JobEndpointsTests : IDisposable
{
    // Same as the API: statuses travel as text ("Applied"), not numbers.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly JoblyApiFactory factory = new();
    private readonly HttpClient client;

    public JobEndpointsTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task GetJobs_NoJobs_ReturnsOkWithEmptyList()
    {
        // Act
        var response = await client.GetAsync("/api/jobs", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<JobResponse>>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Empty(jobs!);
    }

    [Fact]
    public async Task GetJobs_WithStatusFilter_ReturnsOnlyThatColumn()
    {
        // Arrange
        var applied = NewJob("Applied one");
        applied.Application.ChangeStatus(ApplicationStatus.Applied, Now);
        await factory.Seed(NewJob("Still saved"), applied);

        // Act
        var response = await client.GetAsync("/api/jobs?status=Applied", TestContext.Current.CancellationToken);

        // Assert
        var jobs = await response.Content.ReadFromJsonAsync<List<JobResponse>>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(["Applied one"], jobs!.Select(job => job.Title));
    }

    [Fact]
    public async Task GetJobs_UnknownStatus_ReturnsBadRequest()
    {
        // Act
        var response = await client.GetAsync("/api/jobs?status=Banana", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJob_JobExists_ReturnsOkWithJob()
    {
        // Arrange
        var job = await AddJob("Developer");

        // Act
        var response = await client.GetAsync($"/api/jobs/{job.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Developer", body!.Title);
        Assert.Equal(ApplicationStatus.Saved, body.Status);
    }

    [Fact]
    public async Task GetJob_UnknownId_ReturnsNotFoundProblemWithErrorCodeAndTraceId()
    {
        // Act
        var response = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert: 404 in the ProblemDetails shape, with the error code and a trace id for finding the logs.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = await ReadJson(response);
        Assert.Equal("Job.NotFound", problem.RootElement.GetProperty("errorCode").GetString());
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task CreateJob_ValidRequest_ReturnsCreatedWithLocationAndStoresJob()
    {
        // Arrange
        var request = new { title = "Backend Developer", company = "Acme", description = "Build APIs.", url = "https://acme.no/job" };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationStatus.Saved, body!.Status);
        Assert.Equal($"/api/jobs/{body.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal(1, await factory.StoredJobCount());
    }

    [Fact]
    public async Task CreateJob_EmptyTitle_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        var request = new { title = "", company = "Acme", description = "Build APIs." };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs", request, TestContext.Current.CancellationToken);

        // Assert: rejected by the [Required] annotation before the service runs.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.StoredJobCount());
    }

    [Fact]
    public async Task CreateJob_UrlIsNotAUrl_ReturnsBadRequest()
    {
        // Arrange
        var request = new { title = "Developer", company = "Acme", description = "Text", url = "not a url" };

        // Act
        var response = await client.PostAsJsonAsync("/api/jobs", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateJob_ValidRequest_ReturnsOkWithChangedJob()
    {
        // Arrange
        var job = await AddJob("Developer");
        var request = new { title = "Senior Developer", company = "Acme", description = "New text" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{job.Id}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Senior Developer", body!.Title);
    }

    [Fact]
    public async Task UpdateJob_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var request = new { title = "Developer", company = "Acme", description = "Text" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{Guid.NewGuid()}", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_AllowedMove_ReturnsOkWithNewStatus()
    {
        // Arrange
        var job = await AddJob("Developer");

        // Act: the status is sent as text.
        var response = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/status", new { status = "Applied" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationStatus.Applied, body!.Status);
    }

    [Fact]
    public async Task ChangeStatus_DisallowedMove_ReturnsConflictWithErrorCode()
    {
        // Arrange: a saved job can't jump straight to Offer.
        var job = await AddJob("Developer");

        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/status", new { status = "Offer" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await ReadJson(response);
        Assert.Equal("Job.InvalidStatusChange", problem.RootElement.GetProperty("errorCode").GetString());

        // The refused move must not have changed anything: read the job back through the API.
        var stored = await client.GetFromJsonAsync<JobResponse>($"/api/jobs/{job.Id}", JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationStatus.Saved, stored!.Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownId_ReturnsNotFound()
    {
        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{Guid.NewGuid()}/status", new { status = "Applied" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateNotes_JobExists_ReturnsOkWithNotes()
    {
        // Arrange
        var job = await AddJob("Developer");

        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{job.Id}/notes", new { notes = "Call on Monday" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Call on Monday", body!.Notes);
    }

    [Fact]
    public async Task UpdateNotes_UnknownId_ReturnsNotFound()
    {
        // Act
        var response = await client.PutAsJsonAsync($"/api/jobs/{Guid.NewGuid()}/notes", new { notes = "x" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteJob_JobExists_ReturnsNoContentAndRemovesJob()
    {
        // Arrange
        var job = await AddJob("Developer");

        // Act
        var response = await client.DeleteAsync($"/api/jobs/{job.Id}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await factory.StoredJobCount());
    }

    [Fact]
    public async Task DeleteJob_UnknownId_ReturnsNotFound()
    {
        // Act
        var response = await client.DeleteAsync($"/api/jobs/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Job NewJob(string title) =>
        Job.Create(title, "Acme", null, "Description", null, JobSource.Manual, null, Now).Value;

    // Puts a job straight into the database, so each test controls its own starting data.
    private async Task<Job> AddJob(string title)
    {
        var job = NewJob(title);
        await factory.Seed(job);
        return job;
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
