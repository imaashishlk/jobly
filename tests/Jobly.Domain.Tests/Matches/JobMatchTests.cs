using Jobly.Domain.Matches;

namespace Jobly.Domain.Tests.Matches;

public sealed class JobMatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_ReturnsMatchWithTrimmedText()
    {
        // Act
        var result = JobMatch.Create(JobId, 72, "  Good fit for the backend work. ", [" C# ", "SQL"], [" Kubernetes "], "qwen2.5:7b", Now);

        // Assert
        Assert.True(result.IsSuccess);
        var match = result.Value;
        Assert.Equal(JobId, match.JobId);
        Assert.Equal(72, match.Score);
        Assert.Equal("Good fit for the backend work.", match.Summary);
        Assert.Equal(["C#", "SQL"], match.MatchingSkills);
        Assert.Equal(["Kubernetes"], match.Gaps);
        Assert.Equal("qwen2.5:7b", match.Model);
        Assert.Equal(Now, match.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_ScoreOnTheBoundary_Succeeds(int score)
    {
        // Act
        var result = JobMatch.Create(JobId, score, "Summary", [], [], "model", Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(score, result.Value.Score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_ScoreOutsideTheRange_ReturnsErrorInsteadOfClamping(int score)
    {
        // Act
        var result = JobMatch.Create(JobId, score, "Summary", [], [], "model", Now);

        // Assert: a made-up score of 140 must never turn into 100 without anyone noticing.
        Assert.True(result.IsFailure);
        Assert.Equal("Match.ScoreOutOfRange", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SummaryIsEmpty_ReturnsSummaryRequired(string summary)
    {
        // Act
        var result = JobMatch.Create(JobId, 50, summary, [], [], "model", Now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Match.SummaryRequired", result.Error.Code);
    }

    [Fact]
    public void Create_SummaryIsVeryLong_IsCutToTheLimit()
    {
        // Act: a model that rambles must not be able to fill the database.
        var result = JobMatch.Create(JobId, 50, new string('s', JobMatch.MaxSummaryLength * 3), [], [], "model", Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(JobMatch.MaxSummaryLength, result.Value.Summary.Length);
    }

    [Fact]
    public void Create_ListsAreVeryLong_KeepsTheFirstEntriesAndCutsEachOne()
    {
        // Arrange
        var manyLongEntries = Enumerable.Range(1, JobMatch.MaxListItems + 10).Select(number => $"{number}-{new string('x', JobMatch.MaxItemLength * 2)}").ToList();

        // Act
        var result = JobMatch.Create(JobId, 50, "Summary", manyLongEntries, manyLongEntries, "model", Now);

        // Assert
        Assert.Equal(JobMatch.MaxListItems, result.Value.MatchingSkills.Count);
        Assert.Equal(JobMatch.MaxListItems, result.Value.Gaps.Count);
        Assert.All(result.Value.MatchingSkills, item => Assert.Equal(JobMatch.MaxItemLength, item.Length));
        Assert.StartsWith("1-", result.Value.MatchingSkills[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ListsHaveBlankEntries_DropsThem()
    {
        // Act
        var result = JobMatch.Create(JobId, 50, "Summary", ["C#", "  ", ""], ["", "Docker"], "model", Now);

        // Assert
        Assert.Equal(["C#"], result.Value.MatchingSkills);
        Assert.Equal(["Docker"], result.Value.Gaps);
    }
}
