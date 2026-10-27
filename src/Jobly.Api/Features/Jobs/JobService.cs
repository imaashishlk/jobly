using Jobly.Api.Common;
using Jobly.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.Features.Jobs;

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

public sealed class JobService(JoblyDbContext dbContext, TimeProvider timeProvider) : IJobService
{
    public async Task<IReadOnlyList<JobResponse>> ListJobs(ApplicationStatus? status, CancellationToken cancellationToken)
    {
        // AsNoTracking: this list is only read, so EF Core doesn't need to watch the rows for changes.
        var query = dbContext.Jobs
            .AsNoTracking()
            .Include(job => job.Application);

        // Filter only when a status was given. The comparison is translated to SQL, so only that column's rows come back.
        var filtered = status is null
            ? query
            : query.Where(job => job.Application.Status == status);

        var jobs = await filtered
            .OrderByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);

        return jobs.Select(JobResponse.From).ToList();
    }

    public async Task<Result<JobResponse>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await FindJob(id, cancellationToken);
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

        dbContext.Jobs.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return JobResponse.From(created.Value);
    }

    public async Task<Result<JobResponse>> UpdateJob(Guid id, UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var job = await FindJob(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        var updated = job.Update(request.Title, request.Company, request.Location, request.Description, request.Url);
        if (updated.IsFailure) return updated.Error;

        // The job came from FindJob, so EF Core already knows what changed. Saving is all that is left to do.
        await dbContext.SaveChangesAsync(cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result<JobResponse>> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var job = await FindJob(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        // The domain decides which moves are allowed. A refused move comes back as a Conflict error.
        var changed = job.Application.ChangeStatus(request.Status, timeProvider.GetUtcNow());
        if (changed.IsFailure) return changed.Error;

        await dbContext.SaveChangesAsync(cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result<JobResponse>> UpdateNotes(Guid id, UpdateNotesRequest request, CancellationToken cancellationToken)
    {
        var job = await FindJob(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        job.Application.UpdateNotes(request.Notes, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return JobResponse.From(job);
    }

    public async Task<Result> DeleteJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await FindJob(id, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        dbContext.Jobs.Remove(job); // The application row is deleted too (cascade, see JobConfiguration).
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // Tracked on purpose (no AsNoTracking): callers change this job and then save, and EF Core saves those changes.
    private Task<Job?> FindJob(Guid id, CancellationToken cancellationToken) =>
        dbContext.Jobs
            .Include(job => job.Application)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
}
