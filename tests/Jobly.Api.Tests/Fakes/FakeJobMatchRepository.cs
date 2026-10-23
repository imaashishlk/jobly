using Jobly.Application.Matches;
using Jobly.Domain.Matches;

namespace Jobly.Api.Tests.Fakes;

/// <summary>An in-memory <see cref="IJobMatchRepository"/> so API tests run without a database.</summary>
public sealed class FakeJobMatchRepository : IJobMatchRepository
{
    private readonly List<JobMatch> matches = [];

    public IReadOnlyList<JobMatch> Matches => matches;

    public Task<JobMatch?> GetByJobId(Guid jobId, CancellationToken cancellationToken) =>
        Task.FromResult(matches.FirstOrDefault(match => match.JobId == jobId));

    public Task Save(JobMatch match, CancellationToken cancellationToken)
    {
        matches.RemoveAll(existing => existing.JobId == match.JobId);
        matches.Add(match);
        return Task.CompletedTask;
    }
}
