using Jobly.Api.Features.Cvs;
using Jobly.Api.Integrations.Llm;
using Jobly.Api.Persistence;
using Jobly.Api.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Jobly.Api.Tests;

/// <summary>
/// Starts the real API in memory for tests, with PostgreSQL swapped for an in-memory SQLite database and the
/// language model and the file reader swapped for fakes.
/// Every API test class uses this instead of <c>WebApplicationFactory&lt;Program&gt;</c> directly.
/// </summary>
public sealed class JoblyApiFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database exists only while its connection is open, so one connection is kept for the whole test.
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    /// <summary>The fake language model. Change its answer, or make it fail, to test how the API reacts.</summary>
    public FakeLlmClient Llm { get; } = new();

    /// <summary>The fake file reader used for CV uploads.</summary>
    public FakeCvTextExtractor CvTextExtractor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs requires a connection string at startup. This one is never used to connect,
        // because the PostgreSQL database is replaced below.
        builder.UseSetting("ConnectionStrings:Jobly", "Host=not-used;Database=not-used");

        connection.Open();

        builder.ConfigureServices(services =>
        {
            // Take out the PostgreSQL setup completely. EF Core 9 and later also keep the options *configuration* that
            // AddDbContext registered, and it would still try to use Npgsql next to SQLite. That type lives in an
            // internal EF Core namespace, so it is found by its name instead of being referenced.
            services.RemoveAll<DbContextOptions<JoblyDbContext>>();
            var oldConfigurations = services
                .Where(service => service.ServiceType.IsGenericType
                    && service.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)
                    && service.ServiceType.GetGenericArguments()[0] == typeof(JoblyDbContext))
                .ToList();
            foreach (var oldConfiguration in oldConfigurations) services.Remove(oldConfiguration);

            services.AddDbContext<JoblyDbContext>(options => options
                .UseSqlite(connection)
                .ReplaceService<IModelCustomizer, StoreDateTimeOffsetAsNumberCustomizer>());

            // Remove each real implementation and put the fake in its place. Singletons, so the test and the API share one instance.
            services.RemoveAll<ILlmClient>();
            services.AddSingleton<ILlmClient>(Llm);

            services.RemoveAll<ICvTextExtractor>();
            services.AddSingleton<ICvTextExtractor>(CvTextExtractor);
        });
    }

    // Creates the tables as soon as the API is built, before the first request.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<JoblyDbContext>().Database.EnsureCreated();

        return host;
    }

    /// <summary>Adds starting data (a job, a CV, ...) straight to the database, so each test controls what the API finds.</summary>
    public async Task Seed(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JoblyDbContext>();

        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The CV that is saved right now, or null. Read with a fresh context, so it shows what is really stored.</summary>
    public async Task<Cv?> StoredCv()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JoblyDbContext>();

        return await dbContext.Cvs.AsNoTracking().SingleOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    public async Task<int> StoredJobCount()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JoblyDbContext>();

        return await dbContext.Jobs.CountAsync(TestContext.Current.CancellationToken);
    }

    public async Task<int> StoredMatchCount()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JoblyDbContext>();

        return await dbContext.JobMatches.CountAsync(TestContext.Current.CancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    // SQLite can't sort by a DateTimeOffset column (PostgreSQL can), and the job list sorts by CreatedAt.
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
