using Jobly.Api.Tests.Fakes;
using Jobly.Application.Cvs;
using Jobly.Application.Integrations.Llm;
using Jobly.Application.Jobs;
using Jobly.Application.Matches;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jobly.Api.Tests;

/// <summary>
/// Starts the real API in memory for tests, with the database, the language model and the file reader swapped for fakes.
/// Every API test class uses this instead of <c>WebApplicationFactory&lt;Program&gt;</c> directly.
/// </summary>
public sealed class JoblyApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fake job storage behind the API, so a test can add jobs before calling an endpoint or check them afterwards.</summary>
    public FakeJobRepository Jobs { get; } = new();

    public FakeCvRepository Cvs { get; } = new();

    public FakeJobMatchRepository Matches { get; } = new();

    /// <summary>The fake language model. Change its answer, or make it fail, to test how the API reacts.</summary>
    public FakeLlmClient Llm { get; } = new();

    /// <summary>The fake file reader used for CV uploads.</summary>
    public FakeCvTextExtractor CvTextExtractor { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs requires a connection string at startup. This one is never used to connect,
        // because the repositories that would use it are replaced below.
        builder.UseSetting("ConnectionStrings:Jobly", "Host=not-used;Database=not-used");

        builder.ConfigureServices(services =>
        {
            // Remove each real implementation and put the fake in its place. Singletons, so the test and the API share one instance.
            services.RemoveAll<IJobRepository>();
            services.AddSingleton<IJobRepository>(Jobs);

            services.RemoveAll<ICvRepository>();
            services.AddSingleton<ICvRepository>(Cvs);

            services.RemoveAll<IJobMatchRepository>();
            services.AddSingleton<IJobMatchRepository>(Matches);

            services.RemoveAll<ILlmClient>();
            services.AddSingleton<ILlmClient>(Llm);

            services.RemoveAll<ICvTextExtractor>();
            services.AddSingleton<ICvTextExtractor>(CvTextExtractor);
        });
    }
}
