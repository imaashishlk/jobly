using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database. Called once from Program.cs.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from the ConnectionStrings__Jobly environment variable (Docker) or user secrets (local).
        // Fails at startup with a clear message when missing, instead of halfway through a user's request.
        var connectionString = configuration.GetConnectionString("Jobly")
            ?? throw new InvalidOperationException("The connection string 'ConnectionStrings:Jobly' is not configured.");

        // AddDbContext registers the context as scoped: one instance per HTTP request.
        // Services ask for JoblyDbContext directly, so there is no repository layer in between.
        services.AddDbContext<JoblyDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
