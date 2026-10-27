using Jobly.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jobly.Api.UnitTests.Fakes;

/// <summary>
/// A real, empty database that lives only in memory (SQLite), so services can be tested with the real
/// <see cref="JoblyDbContext"/> instead of a fake repository. Create one per test and dispose it at the end.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    // An in-memory SQLite database exists only while its connection is open, so we keep one connection for the whole test.
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly DbContextOptions<JoblyDbContext> options;

    public TestDatabase()
    {
        connection.Open();

        options = new DbContextOptionsBuilder<JoblyDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, StoreDateTimeOffsetAsNumberCustomizer>()
            .Options;

        // Creates the tables from the EF Core model. The Npgsql-specific parts are covered by JoblyDbContextModelTests.
        using var setup = NewContext();
        setup.Database.EnsureCreated();

        Context = NewContext();
    }

    /// <summary>The context to give to the service under test, and to add starting data with.</summary>
    public JoblyDbContext Context { get; }

    /// <summary>
    /// A fresh context on the same database. Use it to check what was really saved: it has tracked nothing,
    /// so it can't show a change that only exists in memory.
    /// </summary>
    public JoblyDbContext NewContext() => new(options);

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }

    // SQLite can't sort by a DateTimeOffset column (PostgreSQL can), and ListJobs sorts by CreatedAt.
    // Test-only workaround: store every DateTimeOffset as a number in this in-memory database.
    // The production model is not touched.
    private sealed class StoreDateTimeOffsetAsNumberCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            var dateColumns = modelBuilder.Model.GetEntityTypes()
                .SelectMany(entityType => entityType.GetProperties())
                .Where(property => property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?));

            foreach (var property in dateColumns)
            {
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
