using Jobly.Domain.Common;

namespace Jobly.Domain.Matches;

/// <summary>All the expected failures for matching a CV with a job.</summary>
public static class MatchErrors
{
    public static readonly Error NotFound = Error.NotFound("Match.NotFound", "This job has not been analyzed yet.");

    // Conflict: the request is fine, but the app is not in the right state yet (add a CV first).
    public static readonly Error CvRequired = Error.Conflict("Match.CvRequired", "Add your CV first, then analyze the job.");

    // The model answered, but not in the shape we asked for. Our fault or the model's, never the caller's.
    public static readonly Error InvalidResponse = Error.Failure("Match.InvalidResponse", "The model's answer could not be used. Please try again.");

    public static readonly Error ScoreOutOfRange = Error.Validation("Match.ScoreOutOfRange", "The score must be between 0 and 100.");

    public static readonly Error SummaryRequired = Error.Validation("Match.SummaryRequired", "The summary is required.");
}
