using Jobly.Domain.Common;
using Jobly.Domain.Jobs;

namespace Jobly.Application.Jobs;

public sealed class JobService(IJobRepository jobRepository, TimeProvider timeProvider) : IJobService
{
    public async Task<IReadOnlyList<JobResponse>> ListJobs(ApplicationStatus? status, CancellationToken cancellationToken)
    {
        var jobs = await jobRepository.GetAll(status, cancellationToken);
        return jobs.Select(JobResponse.From).ToList();
    }

    public async Task<Result<JobResponse>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        return JobResponse.From(job);
    }

    public async Task<Result<JobResponse>> CreateJob(CreateJobRequest request, CancellationToken cancellationToken)
    {
        // Jobs entered through the API are always Manual. Arbeidsplassen jobs will come from the import (a later plan).
        var created = Job.Create(
            request.Title,
            request.Company,
            request.Location,
            request.Description,
            request.Url,
            JobSource.Manual,
            externalId: null,
            timeProvider.GetUtcNow());

        if (created.IsFailure) return created.Error;

        await jobRepository.Add(created.Value, cancellationToken);

        return JobResponse.From(created.Value);
    }

    public async Task<Result<JobResponse>> UpdateJob(Guid id, UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        var updated = job.Update(request.Title, request.Company, request.Location, request.Description, request.Url);
        if (updated.IsFailure) return updated.Error;

        await jobRepository.Update(job, cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result<JobResponse>> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        // The domain decides which moves are allowed. A refused move comes back as a Conflict error.
        var changed = job.Application.ChangeStatus(request.Status, timeProvider.GetUtcNow());
        if (changed.IsFailure) return changed.Error;

        await jobRepository.Update(job, cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result<JobResponse>> UpdateNotes(Guid id, UpdateNotesRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        job.Application.UpdateNotes(request.Notes, timeProvider.GetUtcNow());
        await jobRepository.Update(job, cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result> DeleteJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        await jobRepository.Delete(job, cancellationToken);

        return Result.Success();
    }
}
