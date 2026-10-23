using Jobly.Domain.Common;
using Jobly.Domain.Jobs;

namespace Jobly.Application.Jobs;

/// <summary>The use cases for saving jobs and tracking them on the Kanban board.</summary>
public interface IJobService
{
    /// <summary>All jobs, newest first. This cannot fail in an expected way, so it returns a plain list.</summary>
    Task<IReadOnlyList<JobResponse>> ListJobs(ApplicationStatus? status, CancellationToken cancellationToken);

    Task<Result<JobResponse>> GetJob(Guid id, CancellationToken cancellationToken);

    Task<Result<JobResponse>> CreateJob(CreateJobRequest request, CancellationToken cancellationToken);

    Task<Result<JobResponse>> UpdateJob(Guid id, UpdateJobRequest request, CancellationToken cancellationToken);

    /// <summary>Moves the job to another column. Fails with a conflict if the rules don't allow that move.</summary>
    Task<Result<JobResponse>> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken cancellationToken);

    Task<Result<JobResponse>> UpdateNotes(Guid id, UpdateNotesRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteJob(Guid id, CancellationToken cancellationToken);
}
