using Jobly.Domain.Jobs;
using Jobly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Infrastructure.Tests.Persistence;

// These tests build the database model for PostgreSQL but never connect to a database:
// "not-used" is only there so the provider accepts the settings.
public sealed class JoblyDbContextModelTests
{
    private static JoblyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<JoblyDbContext>()
            .UseNpgsql("Host=not-used;Database=not-used")
            .Options;

        return new JoblyDbContext(options);
    }

    [Fact]
    public void GenerateCreateScript_PostgresProvider_UsesPostgresTypesForBothTables()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert: Guid, UTC dates and long text map to the native PostgreSQL types.
        Assert.Contains("CREATE TABLE \"Jobs\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE \"JobApplications\"", script, StringComparison.Ordinal);
        Assert.Contains("uuid", script, StringComparison.Ordinal);
        Assert.Contains("timestamp with time zone", script, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateCreateScript_PostgresProvider_HasNoSqlServerSyntax()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert: square brackets and nvarchar are SQL Server syntax. A leftover (for example an index filter) would fail in PostgreSQL.
        Assert.DoesNotContain("[", script, StringComparison.Ordinal);
        Assert.DoesNotContain("nvarchar", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Model_JobEntity_HasUniqueIndexOnSourceAndExternalId()
    {
        // Arrange
        using var context = CreateContext();
        var jobType = context.Model.FindEntityType(typeof(Job))!;

        // Act
        var index = jobType.GetIndexes().Single(candidate => candidate.Properties.Select(property => property.Name).SequenceEqual([nameof(Job.Source), nameof(Job.ExternalId)]));

        // Assert: stops the same Arbeidsplassen ad from being saved twice later.
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Model_JobApplicationEntity_HasUniqueIndexOnJobIdAndPlainIndexOnStatus()
    {
        // Arrange
        using var context = CreateContext();
        var applicationType = context.Model.FindEntityType(typeof(JobApplication))!;

        // Act
        var jobIdIndex = applicationType.GetIndexes().Single(candidate => candidate.Properties.Single().Name == nameof(JobApplication.JobId));
        var statusIndex = applicationType.GetIndexes().Single(candidate => candidate.Properties.Single().Name == nameof(JobApplication.Status));

        // Assert: one application per job, and a fast lookup per Kanban column.
        Assert.True(jobIdIndex.IsUnique);
        Assert.False(statusIndex.IsUnique);
    }
}
