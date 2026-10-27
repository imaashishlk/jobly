# Code style

Each rule has a **Why** so the reasoning is clear, not just the rule.

## Design

### Keep code simple. No clever tricks.
**Why:** Code is read far more often than it is written. Code a junior developer understands on first read is easy to review, debug, and change safely.

### Follow SOLID. Depend on interfaces at real boundaries (the LLM, the file reader), not concrete classes.
**Why:** If `MatchAgent` depends on `ILlmClient` instead of `OllamaLlmClient`, you can swap the provider, or use a fake in a unit test, without touching `MatchAgent`. That is what "loosely coupled" means in practice. Inside the app, services use `JoblyDbContext` directly: an interface in front of every class would only add files (see [architecture.md](architecture.md)).

### Register services and integrations with dependency injection.
**Why:** Classes don't create their own dependencies with `new`. They ask for them in the constructor and ASP.NET Core supplies them. Wiring lives in one place (the composition root in `Jobly.Api`), and tests can pass in fakes.

### Mark classes `sealed` unless they are designed for inheritance.
**Why:** It states intent: this class is not meant to be extended. It stops accidental inheritance that couples code together, and lets the runtime optimize calls slightly.

### Use meaningful names with no abbreviations. Keep methods small, each doing one job.
**Why:** `CalculateApplicationDeadline` explains itself, and `CalcAppDl` does not. Small methods with one job are easy to name, test, and reuse.

## Control flow and errors

### Prefer `switch` statements and expressions over `if`/`else` chains.
**Why:** A `switch` lays every case out in one place. With switch expressions over enums or types, the compiler warns when a case is missing. A long `if`/`else` chain hides missing cases until runtime.

**Exception: guard clauses.** A single early return such as `if (job is null) return NotFound();` stays an `if`. Any branching with two or more outcomes uses `switch`.
**Why:** A guard clause has only one outcome, so there's nothing for a `switch` to lay out, and wrapping it in one adds noise.

### Use the Result pattern for expected failures. Exceptions are for the unexpected only.
**Why:** "Job not found" or "title is empty" are normal outcomes, not crashes. Returning `Result<T>` (success with a value, or failure with an `Error`) has three benefits:
- The failure is visible in the method signature, so callers can't forget to handle it.
- Control flow reads top to bottom instead of jumping to a distant `catch`.
- It avoids exceptions, which are expensive to throw and catch.

**How:**
- We write a small `Result` / `Result<T>` / `Error` type ourselves in `Jobly.Api/Common/`, with no package.
- `Error` has a `Code` (for example `Job.NotFound`) and a `Message`.
- The API layer maps errors to HTTP status codes with a `switch`.
- Truly unexpected exceptions (database down, bugs) are caught once by a global exception handler (`IExceptionHandler`), logged with the trace ID, and returned as `ProblemDetails`.
- Never use `try`/`catch` just to hide an error.

## Async

### Never write `async void`. Always return `async Task` or `async Task<T>`.
**Why:** An `async void` method cannot be awaited, so the caller can't know when it finished. An exception thrown inside it can't be caught by the caller and may crash the process. A `Task` carries both completion and exceptions back to the caller.

### Make every database and HTTP call `async`/`await`.
**Why:** While waiting on I/O, the thread goes back to the pool to serve other requests instead of sitting blocked. Never use `.Result` or `.Wait()` on a task: they block the thread and can deadlock.

### Accept a `CancellationToken` in every async method and endpoint handler, and pass it all the way down.
**Why:** If a user closes the browser or a request times out, the token tells EF Core, `HttpClient`, and LLM calls to stop. Without it, the server keeps doing work nobody will receive, which wastes database connections and paid LLM tokens. A token dropped halfway down the chain makes everything below it uncancellable.

## Dependencies on the outside world

### Never create `HttpClient` with `new`. Always use `IHttpClientFactory`, through typed clients.
**Why:**
- Creating and disposing `HttpClient` per call can exhaust sockets under load.
- Keeping one forever misses DNS changes.
- `IHttpClientFactory` manages connection lifetimes correctly.
- It gives each integration one place to configure the base URL, headers, and timeouts.
- It plugs into logging and tracing automatically.

### Never use `DateTime.Now` or `DateTime.UtcNow` directly. Inject `TimeProvider` and call `GetUtcNow()`.
**Why:** Code that reads the system clock can't be tested reliably, because "is this job posting expired?" gives a different answer every day. `TimeProvider` is built into .NET 8+. Production registers `TimeProvider.System`, and tests pass a fake clock fixed at a known time. Store and compare times in UTC (`DateTimeOffset`) to avoid time zone and daylight-saving bugs.

## Data and API boundaries

### Build the API with Minimal APIs: one static class per feature in `Jobly.Api/Features/<Feature>/`, with a `Map<Feature>Endpoints()` extension method.
```csharp
// Program.cs stays a short, readable list of features:
app.MapJobEndpoints();
app.MapApplicationEndpoints();
```
**Why:** Each endpoint is a plain method you can read top to bottom, with no hidden controller machinery. Grouping by feature means everything about "jobs" is in one folder, and `Program.cs` shows the whole API at a glance. Access rules live on the route group (see [security.md](security.md)).

### Handlers return `TypedResults`, with the possible responses in the return type.
```csharp
// The return type lists every response this endpoint can give: 200 with a job, 404, or 403.
private static async Task<Results<Ok<JobResponse>, NotFound, ForbidHttpResult>> GetJob(Guid id, IJobService jobService, CancellationToken cancellationToken)
```
**Why:** You see every possible response in the signature, the compiler checks you only return those, and Swagger/OpenAPI documents them automatically. Unit tests can check the result type directly.

### Result errors become HTTP responses in one shared helper, using a `switch` on the error type.
**Why:** Every endpoint turns `Job.NotFound` into `404` and `Job.NotOwner` into `403` the same way. The mapping is written once, so it can't drift between endpoints.

### Use DTOs for all API input and output. Never return EF Core entities.
**Why:**
- Entities are shaped for the database. Returning them leaks internal fields (password hashes, internal IDs).
- They can cause infinite JSON loops through navigation properties.
- They tie the public API to the database schema, so every schema change breaks clients.

### Validate input in the API layer, using built-in data annotations on request DTOs.
Use `[Required]`, `[MaxLength]`, `[Range]`, `[EmailAddress]`, and similar. In .NET 10, Minimal APIs check them automatically once `builder.Services.AddValidation()` is called in `Program.cs`, and return a `400` with `ValidationProblemDetails`. No validation package.
**Why:**
- Bad input is rejected at the door with a clear `400 Bad Request`, before it reaches business logic or the database, so inner layers can trust what they receive.
- Annotations sit right on the DTO, so you can read the rules where the data is defined.
- Business rules ("a job can't close before it opens") belong in the feature's entities (for example `Job.Update`) and come back as `Result` errors, not annotations.

### Write LINQ queries that stay readable.
**Why:** EF Core translates LINQ to SQL. A clear query is easier to check for performance problems, such as loading too much data or N+1 queries. Project to DTOs with `.Select(...)` so only the needed columns are fetched.

### Never edit an existing migration. Add a new one.
**Why:** Once a migration has run against any database (a teammate's, staging, production), changing it makes that database's history disagree with the code. A new migration moves every database forward the same way.

## Comments

### Add a short comment wherever code might confuse a developer.
**Why:** The goal is that you can read every file and understand it. Comment the *why*, not the obvious *what*. Things that always deserve a comment:
- Dependency injection registrations
- Middleware order
- Result-to-HTTP mapping
- `CancellationToken` plumbing
- EF Core configuration
- LINQ that isn't trivial
- Every log statement (see [logging.md](logging.md))

Don't comment `i++`.

## Packages and secrets

### Don't add NuGet or npm packages without asking.
**Why:** Every package is code we don't control. It can bring security issues, licensing questions, and upgrade work. Often the built-in .NET feature is enough.

### Never commit secrets. Use user secrets locally and environment variables when deployed.
**Why:** Anything committed to git stays in its history forever, even after deletion, and leaked keys get abused fast. Environment variables and user secrets keep secrets outside the repository.
