using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobly.Api.Common;
using Jobly.Api.Features.Cvs;
using Jobly.Api.Features.Jobs;
using Jobly.Api.Features.Matches;
using Jobly.Api.Integrations.Llm;

namespace Jobly.Api.Tests.Endpoints;

// A new in-memory API per test, so jobs, the CV and results from one test never reach another.
public sealed class MatchEndpointsTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly JoblyApiFactory factory = new();
    private readonly HttpClient client;

    public MatchEndpointsTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task PostMatch_JobAndCvExist_ReturnsOkWithScoreSkillsAndGaps()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("Ola Nordmann, C# developer");

        // Act
        var response = await client.PostAsync($"/api/jobs/{job.Id}/match", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobMatchResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(72, body!.Score);
        Assert.Equal(["C#", "SQL"], body.MatchingSkills);
        Assert.Equal(["Kubernetes"], body.Gaps);
        Assert.Equal(1, await factory.StoredMatchCount());
    }

    [Fact]
    public async Task PostMatch_NoCvYet_ReturnsConflictWithErrorCodeAndDoesNotCallTheModel()
    {
        // Arrange
        var job = await AddJob();

        // Act
        var response = await client.PostAsync($"/api/jobs/{job.Id}/match", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Match.CvRequired", await ReadErrorCode(response));
        Assert.Equal(0, factory.Llm.CallCount);
    }

    [Fact]
    public async Task PostMatch_UnknownJob_ReturnsNotFound()
    {
        // Arrange
        await AddCv("CV");

        // Act
        var response = await client.PostAsync($"/api/jobs/{Guid.NewGuid()}/match", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Job.NotFound", await ReadErrorCode(response));
    }

    [Fact]
    public async Task PostMatch_ModelIsDown_ReturnsServiceUnavailableWithMessageAndTraceId()
    {
        // Arrange: Ollama is not running.
        var job = await AddJob();
        await AddCv("CV");
        factory.Llm.Result = Result.Failure<LlmResponse>(Error.Unavailable("Llm.Unavailable", "The language model cannot be reached. Is Ollama running?"));

        // Act
        var response = await client.PostAsync($"/api/jobs/{job.Id}/match", content: null, TestContext.Current.CancellationToken);

        // Assert: 503 with a friendly message and a trace id for finding the logs.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Llm.Unavailable", problem.RootElement.GetProperty("errorCode").GetString());
        Assert.Contains("Ollama", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(0, await factory.StoredMatchCount());
    }

    [Fact]
    public async Task PostMatch_ModelAnswersWithNonsense_ReturnsErrorWithInvalidResponseCode()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("CV");
        factory.Llm.AnswerWith("I think this is a great match!");

        // Act
        var response = await client.PostAsync($"/api/jobs/{job.Id}/match", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Match.InvalidResponse", await ReadErrorCode(response));
    }

    [Fact]
    public async Task GetMatch_JobWasAnalyzed_ReturnsTheSavedResult()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("CV");
        await client.PostAsync($"/api/jobs/{job.Id}/match", content: null, TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetAsync($"/api/jobs/{job.Id}/match", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobMatchResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(72, body!.Score);
    }

    [Fact]
    public async Task GetMatch_JobNotAnalyzedYet_ReturnsNotFound()
    {
        // Arrange
        var job = await AddJob();

        // Act
        var response = await client.GetAsync($"/api/jobs/{job.Id}/match", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Match.NotFound", await ReadErrorCode(response));
    }

    private async Task<Job> AddJob()
    {
        var job = Job.Create("Backend Developer", "Acme AS", null, "Build APIs in C#.", null, JobSource.Manual, null, Now).Value;
        await factory.Seed(job);
        return job;
    }

    private Task AddCv(string text) => factory.Seed(Cv.Create(text, CvSource.Pasted, null, Now).Value);

    private static async Task<string?> ReadErrorCode(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return problem.RootElement.GetProperty("errorCode").GetString();
    }
}
