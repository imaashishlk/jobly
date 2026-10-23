using Jobly.Application.Jobs;
using Jobly.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Infrastructure.Persistence;

/// <summary>Stores jobs in PostgreSQL through EF Core. This is the real <see cref="IJobRepository"/>.</summary>
internal sealed class SqlJobRepository(JoblyDbContext dbContext) : IJobRepository
{
    public async Task<IReadOnlyList<Job>> GetAll(ApplicationStatus? status, CancellationToken cancellationToken)
    {
        // AsNoTracking: this list is only read, so EF Core doesn't need to watch the rows for changes.
        var query = dbContext.Jobs
            .AsNoTracking()
            .Include(job => job.Application);

        // Filter only when a status was given. The comparison is translated to SQL, so only that column's rows come back.
        var filtered = status is null
            ? query
            : query.Where(job => job.Application.Status == status);

        return await filtered
            .OrderByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    // Tracked on purpose (no AsNoTracking): the service changes this job and then calls Update, which saves those changes.
    public Task<Job?> GetById(Guid id, CancellationToken cancellationToken) =>
        dbContext.Jobs
            .Include(job => job.Application)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task Add(Job job, CancellationToken cancellationToken)
    {
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // The job came from GetById, so EF Core already knows what changed. Saving is all that is left to do.
    public Task Update(Job job, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task Delete(Job job, CancellationToken cancellationToken)
    {
        dbContext.Jobs.Remove(job); // The application row is deleted too (cascade, see JobConfiguration).
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
