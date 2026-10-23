using Jobly.Application.Jobs;
using Jobly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobly.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database and the repositories. Called once from Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from the ConnectionStrings__Jobly environment variable (Docker) or user secrets (local).
        // Fails at startup with a clear message when missing, instead of halfway through a user's request.
        var connectionString = configuration.GetConnectionString("Jobly")
            ?? throw new InvalidOperationException("The connection string 'ConnectionStrings:Jobly' is not configured.");

        // AddDbContext registers the context as scoped: one instance per HTTP request.
        services.AddDbContext<JoblyDbContext>(options => options.UseSqlServer(connectionString));

        // Scoped as well, because the repository uses the scoped DbContext.
        services.AddScoped<IJobRepository, SqlJobRepository>();

        return services;
    }
}
