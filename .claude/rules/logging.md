# Logging and observability

**Goal:** when something goes wrong, including an LLM call that is slow, expensive, or failing, you can find out what happened from the logs, follow a single request end to end, and view it all in a dashboard.

## Rules

### Use the built-in `ILogger<T>` everywhere. Inject it, never create it.
**Why:** `ILogger` is part of .NET, needs no package, and is what every observability tool plugs into. Code logs through one interface, and where the logs end up (console, a dashboard, Azure) is configuration, not code.

### Use message templates with named placeholders. Never use string interpolation.
```csharp
// Good: JobId and ElapsedMilliseconds become searchable fields in a dashboard
logger.LogInformation("Job {JobId} created in {ElapsedMilliseconds} ms", job.Id, elapsed);

// Bad: one flat string, which can only be text-searched
logger.LogInformation($"Job {job.Id} created in {elapsed} ms");
```
**Why:** This is *structured logging*. Each placeholder is stored as a separate field, so a dashboard can answer questions like "all logs for JobId 42" or "LLM calls slower than 5000 ms". Interpolation flattens everything into text.

### Every log statement gets a short comment explaining why it is there.
**Why:** It makes the logs part of the code you can read and learn from. Later you'll know what each log tells you when you see it in a dashboard.

### Use the right log level.
| Level | Use for | Example |
|---|---|---|
| `Trace` / `Debug` | Details for local troubleshooting. Off in production. | "Built LLM prompt with {TokenCount} tokens" |
| `Information` | Normal, meaningful events | "Job {JobId} created" |
| `Warning` | Something unexpected but handled | "LLM call retried after timeout" |
| `Error` | An operation failed | "LLM provider returned {StatusCode}" |
| `Critical` | The app can't keep working | "Cannot connect to database at startup" |

**Why:** Levels let production keep only what matters (Information and above) and let alerts fire only on Error and Critical. If everything is logged as Information, the important lines drown.

### Every request is traceable by its trace ID.
**Why:** ASP.NET Core already gives each request a trace ID (W3C `traceparent`). It flows automatically through `ILogger`, `HttpClient` (via `IHttpClientFactory`), and EF Core. Error responses (`ProblemDetails`) include the `traceId`. When a user reports a problem, that ID finds every log line for their request across the API, the database, and the LLM call.

### Add context with logging scopes, not by repeating fields.
```csharp
// Every log line inside this block automatically carries UserId, without repeating it
using (logger.BeginScope(new Dictionary<string, object> { ["UserId"] = userId }))
{
    ...
}
```
**Why:** It keeps log calls short and makes sure related lines can be grouped together.

### Never log secrets or personal data.
That means no passwords, tokens, API keys, connection strings, full CVs, or email bodies.
**Why:** Logs are copied to many places (files, dashboards, cloud services) and kept for a long time. Once sensitive data is in logs, it's very hard to remove.

### LLM calls log metadata, not content.
Log the integration name, model, latency, input and output token counts, success or failure, and the error code. Don't log full prompts or responses: they can contain personal data from CVs and job applications. If we need content for debugging, we decide that explicitly.
**Why:** Token counts and latency are what you need to watch LLM cost and performance. The content is a privacy risk.

## Dashboard (planned, needs package approval)

To view logs, traces, and metrics in a dashboard, the plan is **OpenTelemetry**, the vendor-neutral standard:

- **Locally:** export to the .NET Aspire Dashboard running in Docker (`docker/observability/`). It's a free web UI for logs, traces, and metrics.
- **In Azure:** export to Azure Monitor / Application Insights, which works with App Service.

**Why OpenTelemetry:** the code emits telemetry once in a standard format, and switching dashboards is a configuration change, not a rewrite.

This needs NuGet packages (for example `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, and ASP.NET Core / HttpClient / EF Core instrumentation). They will be proposed in a plan for your approval before anything is added.

## Where logging setup lives

- Logging and telemetry configuration: `src/Jobly.Api/Observability/` (one extension method, `AddObservability()`, called from `Program.cs`).
- Each integration logs through its own `ILogger<T>`, with the integration name in the scope (see [integrations.md](integrations.md)).
