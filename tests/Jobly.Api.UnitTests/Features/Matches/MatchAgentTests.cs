using Jobly.Api.Common;
using Jobly.Api.Features.Cvs;
using Jobly.Api.Features.Jobs;
using Jobly.Api.Features.Matches;
using Jobly.Api.Integrations.Llm;
using Jobly.Api.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.UnitTests.Features.Matches;

public sealed class MatchAgentTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();
    private readonly FakeLlmClient llm = new();
    private readonly MatchAgent agent;

    public MatchAgentTests()
    {
        agent = new MatchAgent(database.Context, llm, new FixedTimeProvider(Now));
    }

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task AnalyzeJob_ValidAnswer_SavesAndReturnsTheMatch()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("Ola Nordmann, C# developer");

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(72, result.Value.Score);
        Assert.Equal("Good fit for the backend work.", result.Value.Summary);
        Assert.Equal(["C#", "SQL"], result.Value.MatchingSkills);
        Assert.Equal(["Kubernetes"], result.Value.Gaps);
        Assert.Equal("fake-model", result.Value.Model);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Single(await StoredMatches());
    }

    [Fact]
    public async Task AnalyzeJob_AnyRun_SendsTheJobAndTheCvToTheModel()
    {
        // Arrange
        var job = await AddJob("Rust Developer");
        await AddCv("Ola Nordmann, C# developer");

        // Act
        await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert
        Assert.Equal(1, llm.CallCount);
        Assert.Contains("Rust Developer", llm.LastRequest!.UserPrompt, StringComparison.Ordinal);
        Assert.Contains("Ola Nordmann, C# developer", llm.LastRequest.UserPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeJob_UnknownJob_ReturnsJobNotFoundWithoutCallingTheModel()
    {
        // Arrange
        await AddCv("CV");

        // Act
        var result = await agent.AnalyzeJob(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.NotFound", result.Error.Code);
        Assert.Equal(0, llm.CallCount);
    }

    [Fact]
    public async Task AnalyzeJob_NoCvYet_ReturnsCvRequiredWithoutCallingTheModel()
    {
        // Arrange
        var job = await AddJob();

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Match.CvRequired", result.Error.Code);
        Assert.Equal(0, llm.CallCount);
    }

    [Fact]
    public async Task AnalyzeJob_ModelIsUnavailable_ReturnsThatErrorUnchangedAndSavesNothing()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("CV");
        var unavailable = Error.Unavailable("Llm.Unavailable", "The model is not reachable.");
        llm.Result = Result.Failure<LlmResponse>(unavailable);

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert: the caller sees "model down" (503), not a vague failure.
        Assert.True(result.IsFailure);
        Assert.Equal(unavailable, result.Error);
        Assert.Empty(await StoredMatches());
    }

    [Theory]
    [InlineData("I think this is a great match!")]
    [InlineData("""{"score":72,"summary":"Good fit."}""")]
    [InlineData("""{"score":"seventy","summary":"Good fit.","matchingSkills":[],"gaps":[]}""")]
    [InlineData("""{"score":72.5,"summary":"Good fit.","matchingSkills":[],"gaps":[]}""")]
    [InlineData("""{"score":140,"summary":"Great.","matchingSkills":[],"gaps":[]}""")]
    [InlineData("""{"score":50,"summary":"  ","matchingSkills":[],"gaps":[]}""")]
    public async Task AnalyzeJob_AnswerCannotBeUsed_ReturnsInvalidResponseAndSavesNothing(string answer)
    {
        // Arrange: not JSON, a missing field, a wrong type, a decimal score, a score of 140, or an empty summary.
        var job = await AddJob();
        await AddCv("CV");
        llm.AnswerWith(answer);

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Match.InvalidResponse", result.Error.Code);
        Assert.Empty(await StoredMatches());
    }

    [Fact]
    public async Task AnalyzeJob_JobAlreadyAnalyzed_ReplacesTheEarlierResult()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("CV");
        await agent.AnalyzeJob(job.Id, CancellationToken.None);
        llm.AnswerWith("""{"score":40,"summary":"Weaker than it looked.","matchingSkills":[],"gaps":["Docker"]}""");

        // Act
        await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert: only the latest result is kept.
        var stored = await StoredMatches();
        Assert.Single(stored);
        Assert.Equal(40, stored[0].Score);
    }

    [Fact]
    public async Task GetMatch_JobWasAnalyzed_ReturnsTheSavedResult()
    {
        // Arrange
        var job = await AddJob();
        await AddCv("CV");
        await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Act
        var result = await agent.GetMatch(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(72, result.Value.Score);
    }

    [Fact]
    public async Task GetMatch_JobNotAnalyzedYet_ReturnsMatchNotFound()
    {
        // Act
        var result = await agent.GetMatch(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Match.NotFound", result.Error.Code);
    }

    private async Task<Job> AddJob(string title = "Backend Developer")
    {
        var job = Job.Create(title, "Acme AS", null, "Build APIs in C#.", null, JobSource.Manual, null, Now).Value;
        database.Context.Jobs.Add(job);
        await database.Context.SaveChangesAsync();
        StartLikeANewRequest();
        return job;
    }

    private async Task AddCv(string text)
    {
        database.Context.Cvs.Add(Cv.Create(text, CvSource.Pasted, null, Now).Value);
        await database.Context.SaveChangesAsync();
        StartLikeANewRequest();
    }

    // In the real API every request gets a fresh DbContext that tracks nothing yet. Forgetting what the test just added
    // makes the agent run the same way. Otherwise EF Core would still be tracking the test's Job and treat a second match
    // for it as a replacement of the first.
    private void StartLikeANewRequest() => database.Context.ChangeTracker.Clear();

    // Reads the saved results through a fresh context, so the test sees what is really stored.
    private async Task<List<JobMatch>> StoredMatches()
    {
        await using var check = database.NewContext();
        return await check.JobMatches.ToListAsync();
    }
}
