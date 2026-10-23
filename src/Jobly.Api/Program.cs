using System.Text.Json.Serialization;
using Jobly.Api.Common;
using Jobly.Api.Endpoints.Cvs;
using Jobly.Api.Endpoints.Jobs;
using Jobly.Api.Endpoints.Matches;
using Jobly.Application.Cvs;
using Jobly.Application.Jobs;
using Jobly.Application.Matches;
using Jobly.Infrastructure;
using Jobly.Infrastructure.Integrations.Llm;
using Microsoft.AspNetCore.Authorization;

// ---------------------------------------------------------------------------------------------
// Program.cs is the composition root: the one place where services are registered (dependency
// injection) and the request pipeline is set up. Business code never lives here.
// ---------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// --- Services ---------------------------------------------------------------------------------

// The system clock. Code asks for TimeProvider instead of reading DateTime.Now, so tests can pass a fixed clock.
builder.Services.AddSingleton(TimeProvider.System);

// Checks [Required], [MaxLength], ... on request DTOs before the endpoint runs, and returns 400 with the details.
builder.Services.AddValidation();

// Enums travel as text in JSON ("Applied", "Manual") instead of numbers, in both directions.
// The client then never depends on the order of the enum values.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Every error response uses the standard ProblemDetails JSON shape, including a traceId for finding the logs.
builder.Services.AddProblemDetails();

// Catches exceptions nobody handled, logs them, and returns a clean 500 (see Common/GlobalExceptionHandler.cs).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks: Docker and Azure call /health to know the app is alive.
builder.Services.AddHealthChecks();

// The database (EF Core + PostgreSQL) and the repositories that use it (see Jobly.Infrastructure/DependencyInjection.cs).
builder.Services.AddInfrastructure(builder.Configuration);

// The language model (Ollama): ILlmClient, its settings from Integrations:Llm, and the typed HTTP client.
builder.Services.AddLlmIntegration(builder.Configuration);

// Scoped: one JobService per request. It asks for IJobRepository, which is the PostgreSQL one registered above.
builder.Services.AddScoped<IJobService, JobService>();

// The CV use cases, and the Match agent that compares the CV with a job using the language model.
builder.Services.AddScoped<ICvService, CvService>();
builder.Services.AddScoped<IMatchAgent, MatchAgent>();

// No endpoint needs a request bigger than a 5 MB CV file plus the form's own overhead. Anything larger is refused
// with 413 before it is read into memory. (Kestrel is the web server that runs inside the API.)
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 6 * 1024 * 1024);

builder.Services.AddAuthorization(options =>
{
    // Safety net: any endpoint without .RequireAuthorization() or .AllowAnonymous() requires a signed-in user,
    // so forgetting to declare access closes an endpoint instead of opening it (see .claude/rules/security.md).
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// --- Request pipeline (order matters: each step wraps the ones below it) ----------------------

// First, so it can catch exceptions thrown anywhere further down the pipeline.
app.UseExceptionHandler();

// Turns empty error responses (for example a 404 for an unknown URL) into ProblemDetails as well.
app.UseStatusCodePages();

// Checks each endpoint's access rules (and the fallback policy) before the endpoint runs.
app.UseAuthorization();

// --- Endpoints --------------------------------------------------------------------------------

app.MapJobEndpoints();
app.MapCvEndpoints();
app.MapMatchEndpoints();

app.MapHealthChecks("/health")
    .AllowAnonymous(); // Public on purpose: Docker and Azure must reach it without signing in. It only says "Healthy".

app.Run();
