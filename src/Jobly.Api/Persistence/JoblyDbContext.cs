using Jobly.Api.Features.Cvs;
using Jobly.Api.Features.Jobs;
using Jobly.Api.Features.Matches;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.Persistence;

/// <summary>
/// The EF Core door to PostgreSQL. Each DbSet is a table.
/// </summary>
public sealed class JoblyDbContext(DbContextOptions<JoblyDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<Cv> Cvs => Set<Cv>();

    public DbSet<JobMatch> JobMatches => Set<JobMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration class in this project. Each one sits in its own feature folder
        // (for example Features/Jobs/JobConfiguration.cs), so a table's rules live next to the entity they describe.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JoblyDbContext).Assembly);
    }
}
