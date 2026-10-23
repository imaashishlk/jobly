using Jobly.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Infrastructure.Persistence;

/// <summary>
/// The EF Core door to PostgreSQL. Each DbSet is a table.
/// </summary>
public sealed class JoblyDbContext(DbContextOptions<JoblyDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration class in this project (see Persistence/Configurations/),
        // so each table's rules live next to each other instead of in one long method.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JoblyDbContext).Assembly);
    }
}
