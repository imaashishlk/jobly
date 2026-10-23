using Jobly.Application.Matches;
using Jobly.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Infrastructure.Persistence;

/// <summary>Stores match results in PostgreSQL through EF Core. Only the latest result per job is kept.</summary>
internal sealed class SqlJobMatchRepository(JoblyDbContext dbContext) : IJobMatchRepository
{
    // AsNoTracking: a result is only read and shown, never changed. A new run creates a new result instead.
    public Task<JobMatch?> GetByJobId(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.JobMatches.AsNoTracking().FirstOrDefaultAsync(match => match.JobId == jobId, cancellationToken);

    public async Task Save(JobMatch match, CancellationToken cancellationToken)
    {
        // Replace the earlier result for this job (the unique index allows only one) in one transaction,
        // so a failed save cannot leave the job without its old result either.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.JobMatches.Where(existing => existing.JobId == match.JobId).ExecuteDeleteAsync(cancellationToken);
        dbContext.JobMatches.Add(match);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
