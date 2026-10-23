using Jobly.Domain.Common;

namespace Jobly.Domain.Matches;

/// <summary>
/// The result of comparing the CV with one job: a score, what matches, and what is missing.
/// Only the latest result per job is kept.
/// </summary>
public sealed class JobMatch
{
    private const int MinScore = 0;
    private const int MaxScore = 100;

    // A model can ramble. These limits keep one answer from filling the database or the screen.
    public const int MaxSummaryLength = 2_000;
    public const int MaxListItems = 20;
    public const int MaxItemLength = 200;

    private JobMatch(Guid jobId, int score, string summary, List<string> matchingSkills, List<string> gaps, string model, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Score = score;
        Summary = summary;
        MatchingSkills = matchingSkills;
        Gaps = gaps;
        Model = model;
        CreatedAt = now;
    }

    // Used by EF Core when it loads a row from the database.
    private JobMatch()
    {
        Summary = string.Empty;
        MatchingSkills = [];
        Gaps = [];
        Model = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    /// <summary>How well the CV fits the job, from 0 (no fit) to 100 (perfect fit).</summary>
    public int Score { get; private set; }

    public string Summary { get; private set; }

    public IReadOnlyList<string> MatchingSkills { get; private set; }

    public IReadOnlyList<string> Gaps { get; private set; }

    /// <summary>The name of the LLM that produced this result, so old results can be told apart after a model change.</summary>
    public string Model { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<JobMatch> Create(
        Guid jobId,
        int score,
        string summary,
        IEnumerable<string> matchingSkills,
        IEnumerable<string> gaps,
        string model,
        DateTimeOffset now)
    {
        // An out-of-range score is an error and is never clamped: a made-up 140 must not quietly become 100.
        if (score is < MinScore or > MaxScore) return MatchErrors.ScoreOutOfRange;
        if (string.IsNullOrWhiteSpace(summary)) return MatchErrors.SummaryRequired;

        return new JobMatch(jobId, score, Cut(summary.Trim(), MaxSummaryLength), Clean(matchingSkills), Clean(gaps), model.Trim(), now);
    }

    // Models sometimes return empty entries or extra spaces in a list. Trim them, drop the empty ones,
    // and keep only the first MaxListItems entries, each cut to MaxItemLength.
    private static List<string> Clean(IEnumerable<string> items) =>
        items
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Take(MaxListItems)
            .Select(item => Cut(item, MaxItemLength))
            .ToList();

    private static string Cut(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
