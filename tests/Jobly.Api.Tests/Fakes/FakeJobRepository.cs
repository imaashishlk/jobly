using Jobly.Application.Jobs;
using Jobly.Domain.Jobs;

namespace Jobly.Api.Tests.Fakes;

/// <summary>An in-memory <see cref="IJobRepository"/> so API tests run without a database.</summary>
public sealed class FakeJobRepository : IJobRepository
{
    private readonly List<Job> jobs = [];

    /// <summary>What is stored right now, so tests can check it.</summary>
    public IReadOnlyList<Job> Jobs => jobs;

    public Task<IReadOnlyList<Job>> GetAll(ApplicationStatus? status, CancellationToken cancellationToken)
    {
        IReadOnlyList<Job> result = jobs
            .Where(job => status is null || job.Application.Status == status)
            .OrderByDescending(job => job.CreatedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Job?> GetById(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(jobs.FirstOrDefault(job => job.Id == id));

    public Task Add(Job job, CancellationToken cancellationToken)
    {
        jobs.Add(job);
        return Task.CompletedTask;
    }

    // Entities are kept by reference, so changes made in the service are already visible here. Nothing to save.
    public Task Update(Job job, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task Delete(Job job, CancellationToken cancellationToken)
    {
        jobs.Remove(job);
        return Task.CompletedTask;
    }
}
