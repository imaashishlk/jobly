using Jobly.Domain.Jobs;

namespace Jobly.Application.Jobs;

/// <summary>
/// What the Application layer needs from storage. Infrastructure implements it with SQL Server,
/// and tests implement it with a fake, so <see cref="JobService"/> never knows which database is used.
/// </summary>
public interface IJobRepository
{
    /// <summary>All jobs with their application, newest first. Pass a status to get only that Kanban column.</summary>
    Task<IReadOnlyList<Job>> GetAll(ApplicationStatus? status, CancellationToken cancellationToken);

    /// <summary>The job with its application, or null if there is none with that id.</summary>
    Task<Job?> GetById(Guid id, CancellationToken cancellationToken);

    Task Add(Job job, CancellationToken cancellationToken);

    /// <summary>Saves changes made to a job (or its application) that was loaded with <see cref="GetById"/>.</summary>
    Task Update(Job job, CancellationToken cancellationToken);

    Task Delete(Job job, CancellationToken cancellationToken);
}
