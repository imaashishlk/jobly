using Jobly.Domain.Matches;

namespace Jobly.Application.Matches;

/// <summary>What the Application layer needs from storage for match results. Only the latest result per job is kept.</summary>
public interface IJobMatchRepository
{
    /// <summary>The latest result for this job, or null if it has not been analyzed yet.</summary>
    Task<JobMatch?> GetByJobId(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Stores this result and replaces any earlier result for the same job.</summary>
    Task Save(JobMatch match, CancellationToken cancellationToken);
}
