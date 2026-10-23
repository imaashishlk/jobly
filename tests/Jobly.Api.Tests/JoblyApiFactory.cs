using Jobly.Api.Tests.Fakes;
using Jobly.Application.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jobly.Api.Tests;

/// <summary>
/// Starts the real API in memory for tests, with the database swapped for a fake.
/// Every API test class uses this instead of <c>WebApplicationFactory&lt;Program&gt;</c> directly.
/// </summary>
public sealed class JoblyApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fake storage behind the API, so a test can add jobs before calling an endpoint or check them afterwards.</summary>
    public FakeJobRepository Jobs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs requires a connection string at startup. This one is never used to connect,
        // because the repository that would use it is replaced below.
        builder.UseSetting("ConnectionStrings:Jobly", "Host=not-used;Database=not-used");

        builder.ConfigureServices(services =>
        {
            // Remove the database repository and put the fake in its place. Singleton, so the test and the API share one instance.
            services.RemoveAll<IJobRepository>();
            services.AddSingleton<IJobRepository>(Jobs);
        });
    }
}
