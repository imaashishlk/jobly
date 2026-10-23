using Jobly.Application.Matches;
using Jobly.Domain.Matches;

namespace Jobly.Application.Tests.Fakes;

/// <summary>An in-memory <see cref="IJobMatchRepository"/> for unit tests.</summary>
public sealed class FakeJobMatchRepository : IJobMatchRepository
{
    private readonly List<JobMatch> matches = [];

    /// <summary>Everything stored right now, so tests can check it.</summary>
    public IReadOnlyList<JobMatch> Matches => matches;

    public Task<JobMatch?> GetByJobId(Guid jobId, CancellationToken cancellationToken) =>
        Task.FromResult(matches.FirstOrDefault(match => match.JobId == jobId));

    // Only the latest result per job is kept, like the real repository.
    public Task Save(JobMatch match, CancellationToken cancellationToken)
    {
        matches.RemoveAll(existing => existing.JobId == match.JobId);
        matches.Add(match);
        return Task.CompletedTask;
    }
}
