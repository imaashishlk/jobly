using Jobly.Domain.Matches;

namespace Jobly.Application.Matches;

/// <summary>What the API returns for a match result.</summary>
public sealed record JobMatchResponse(
    Guid JobId,
    int Score,
    string Summary,
    IReadOnlyList<string> MatchingSkills,
    IReadOnlyList<string> Gaps,
    string Model,
    DateTimeOffset CreatedAt)
{
    public static JobMatchResponse From(JobMatch match) =>
        new(match.JobId, match.Score, match.Summary, match.MatchingSkills, match.Gaps, match.Model, match.CreatedAt);
}
