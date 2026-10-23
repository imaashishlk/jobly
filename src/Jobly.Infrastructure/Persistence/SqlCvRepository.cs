using Jobly.Application.Cvs;
using Jobly.Domain.Cvs;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Infrastructure.Persistence;

/// <summary>Stores the CV in PostgreSQL through EF Core. There is only ever one CV.</summary>
internal sealed class SqlCvRepository(JoblyDbContext dbContext) : ICvRepository
{
    // Tracked on purpose (no AsNoTracking): the service may change the text and then call Save, which saves those changes.
    public Task<Cv?> Get(CancellationToken cancellationToken) =>
        dbContext.Cvs.FirstOrDefaultAsync(cancellationToken);

    public async Task Save(Cv cv, CancellationToken cancellationToken)
    {
        // Make this CV the only one: every other row goes, and the new one is added. Both happen in one
        // transaction, so a failure cannot leave the user with no CV at all.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Cvs.Where(other => other.Id != cv.Id).ExecuteDeleteAsync(cancellationToken);

        // A CV loaded from the database is already tracked and only needs saving. A new one must be added.
        if (dbContext.Entry(cv).State == EntityState.Detached) dbContext.Cvs.Add(cv);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task Delete(Cv cv, CancellationToken cancellationToken)
    {
        dbContext.Cvs.Remove(cv);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
