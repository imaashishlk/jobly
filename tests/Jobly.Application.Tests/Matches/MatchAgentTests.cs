using Jobly.Application.Integrations.Llm;
using Jobly.Application.Matches;
using Jobly.Application.Tests.Fakes;
using Jobly.Domain.Common;
using Jobly.Domain.Cvs;
using Jobly.Domain.Jobs;
using Jobly.Domain.Matches;

namespace Jobly.Application.Tests.Matches;

public sealed class MatchAgentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeJobRepository jobs = new();
    private readonly FakeCvRepository cvs = new();
    private readonly FakeJobMatchRepository matches = new();
    private readonly FakeLlmClient llm = new();
    private readonly MatchAgent agent;

    public MatchAgentTests()
    {
        agent = new MatchAgent(jobs, cvs, matches, llm, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task AnalyzeJob_ValidAnswer_SavesAndReturnsTheMatch()
    {
        // Arrange
        var job = await AddJob();
        cvs.Current = Cv.Create("Ola Nordmann, C# developer", CvSource.Pasted, null, Now).Value;

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
        Assert.Single(matches.Matches);
    }

    [Fact]
    public async Task AnalyzeJob_AnyRun_SendsTheJobAndTheCvToTheModel()
    {
        // Arrange
        var job = await AddJob("Rust Developer");
        cvs.Current = Cv.Create("Ola Nordmann, C# developer", CvSource.Pasted, null, Now).Value;

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
        cvs.Current = Cv.Create("CV", CvSource.Pasted, null, Now).Value;

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
        cvs.Current = Cv.Create("CV", CvSource.Pasted, null, Now).Value;
        var unavailable = Error.Unavailable("Llm.Unavailable", "The model is not reachable.");
        llm.Result = Result.Failure<LlmResponse>(unavailable);

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert: the caller sees "model down" (503), not a vague failure.
        Assert.True(result.IsFailure);
        Assert.Equal(unavailable, result.Error);
        Assert.Empty(matches.Matches);
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
        cvs.Current = Cv.Create("CV", CvSource.Pasted, null, Now).Value;
        llm.AnswerWith(answer);

        // Act
        var result = await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Match.InvalidResponse", result.Error.Code);
        Assert.Empty(matches.Matches);
    }

    [Fact]
    public async Task AnalyzeJob_JobAlreadyAnalyzed_ReplacesTheEarlierResult()
    {
        // Arrange
        var job = await AddJob();
        cvs.Current = Cv.Create("CV", CvSource.Pasted, null, Now).Value;
        await agent.AnalyzeJob(job.Id, CancellationToken.None);
        llm.AnswerWith("""{"score":40,"summary":"Weaker than it looked.","matchingSkills":[],"gaps":["Docker"]}""");

        // Act
        await agent.AnalyzeJob(job.Id, CancellationToken.None);

        // Assert: only the latest result is kept.
        Assert.Single(matches.Matches);
        Assert.Equal(40, matches.Matches[0].Score);
    }

    [Fact]
    public async Task GetMatch_JobWasAnalyzed_ReturnsTheSavedResult()
    {
        // Arrange
        var job = await AddJob();
        cvs.Current = Cv.Create("CV", CvSource.Pasted, null, Now).Value;
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
        await jobs.Add(job, CancellationToken.None);
        return job;
    }
}
