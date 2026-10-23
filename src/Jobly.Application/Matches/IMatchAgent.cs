using Jobly.Domain.Common;

namespace Jobly.Application.Matches;

/// <summary>The Match agent: compares the user's CV with a job using a language model.</summary>
public interface IMatchAgent
{
    /// <summary>
    /// Asks the model how well the CV fits the job and stores the answer, replacing any earlier one.
    /// Fails with <c>Match.CvRequired</c> when there is no CV, and with the model's own error when it is down.
    /// </summary>
    Task<Result<JobMatchResponse>> AnalyzeJob(Guid jobId, CancellationToken cancellationToken);

    /// <summary>The latest stored result for the job, without calling the model.</summary>
    Task<Result<JobMatchResponse>> GetMatch(Guid jobId, CancellationToken cancellationToken);
}
