using System.Text.Json.Serialization;
using Jobly.Api.Common;
using Jobly.Api.Endpoints.Jobs;
using Jobly.Application.Jobs;
using Jobly.Infrastructure;
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

// The database (EF Core + SQL Server) and the repositories that use it (see Jobly.Infrastructure/DependencyInjection.cs).
builder.Services.AddInfrastructure(builder.Configuration);

// Scoped: one JobService per request. It asks for IJobRepository, which is the SQL Server one registered above.
builder.Services.AddScoped<IJobService, JobService>();

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

app.MapHealthChecks("/health")
    .AllowAnonymous(); // Public on purpose: Docker and Azure must reach it without signing in. It only says "Healthy".

app.Run();
