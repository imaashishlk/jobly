using Jobly.Api.Features.Jobs;
using Jobly.Api.Features.Matches;
using Jobly.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.UnitTests.Persistence;

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
    public void Model_MatchesMigrationSnapshot_HasNoPendingChanges()
    {
        // Arrange
        using var context = CreateContext();

        // Act: compares the code's model with the model the migrations were generated from. Still no connection to a database.
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert: moving the code into feature folders must not change the database. If this fails, the model changed
        // and a new migration is needed (never edit an existing one).
        Assert.False(hasPendingChanges);
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
    public void GenerateCreateScript_CvAndMatchTables_ExistWithTheExpectedColumns()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert: the CV text has a length limit, and the skill lists are stored as plain text (JSON).
        Assert.Contains("CREATE TABLE \"Cvs\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE \"JobMatches\"", script, StringComparison.Ordinal);
        Assert.Contains("\"Text\" character varying(30000) NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("\"MatchingSkills\" text NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("\"Gaps\" text NOT NULL", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Model_JobMatchEntity_HasOneResultPerJobAndIsDeletedWithItsJob()
    {
        // Arrange
        using var context = CreateContext();
        var matchType = context.Model.FindEntityType(typeof(JobMatch))!;

        // Act
        var jobIdIndex = matchType.GetIndexes().Single(candidate => candidate.Properties.Single().Name == nameof(JobMatch.JobId));
        var foreignKey = matchType.GetForeignKeys().Single(candidate => candidate.PrincipalEntityType.ClrType == typeof(Job));

        // Assert: only the latest result per job is kept, and removing a job removes its result.
        Assert.True(jobIdIndex.IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
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
