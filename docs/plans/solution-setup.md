# Plan: solution setup

**Branch:** `feature/solution-setup`
**Status:** Approved 2026-10-19

## Goal

Create the skeleton every feature builds on: the clean-architecture projects and their references, shared build settings, the first building blocks written test-first (`Result`/`Error`, error-to-HTTP mapping, exception handling, health check, fallback authorization policy), and the Docker setup.

No business features: no jobs, no database tables, no sign-in. Those each get their own plan.

## Repository-level files

| File | Purpose | Why |
|---|---|---|
| `global.json` | Pins the .NET SDK to 10.0.401 (newer feature bands allowed) | Local builds and GitHub Actions use the same SDK. |
| `Directory.Build.props` | Shared settings: `net10.0`, nullable, implicit usings, warnings treated as errors | Written once. A warning can't be quietly ignored. |
| `Directory.Packages.props` | Central package management: every NuGet version in one file | Every package and version is visible in one place, which makes approvals easy to review. |
| `.editorconfig` | Formatting and style rules for `dotnet format` | Consistent style without discussion. |
| `.gitattributes` | Line endings | Removes the CRLF warnings and keeps Linux line endings for Docker files. |
| `.dockerignore` | Keeps `bin/`, `obj/`, `.git/`, `.env`, tests and docs out of Docker builds | Faster builds, smaller and safer images. |
| `Jobly.slnx` | The solution, in the XML format that is the default in .NET 10 | Short and readable. |

## Projects

```
src/
  Jobly.Domain/          → no references
  Jobly.Application/     → Domain
  Jobly.Infrastructure/  → Application, Domain
  Jobly.Api/             → Application, Infrastructure   (Microsoft.NET.Sdk.Web, Minimal APIs)
tests/
  Jobly.Domain.Tests/        → Domain
  Jobly.Application.Tests/   → Application
  Jobly.Api.Tests/           → Api
```

Project files are written by hand, not from templates, so no sample files are generated that would have to be deleted.

## Packages (test projects only)

| Package | Version | Why |
|---|---|---|
| `xunit.v3` | 4.0.2 | The test framework (current major version). |
| `xunit.runner.visualstudio` | 4.0.1 | Lets `dotnet test` and VS Code find and run the tests. |
| `Microsoft.NET.Test.Sdk` | 18.11.0 | The .NET test host `dotnet test` needs. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | `WebApplicationFactory`, which runs the real API in memory for integration tests. Pinned to match the installed ASP.NET Core runtime (10.0.12). |

No packages in `src/`.

## Tasks (test first, one at a time)

1. **`Result` and `Error`** in `Jobly.Domain/Common/`. `Error` is a record with `Code`, `Message` and `ErrorType`. `Result` and `Result<T>` hold success or failure, and reading the value of a failure throws. Tests in `Jobly.Domain.Tests`.
2. **Error → HTTP mapping** in `Jobly.Api/Common/ResultExtensions.cs`. A `switch` on `ErrorType` maps Validation→400, Unauthorized→401, Forbidden→403, NotFound→404, Conflict→409 and Failure→500, returned as `ProblemDetails`. One test per type.
3. **`Program.cs`:**
   - `AddValidation()` (data annotations, .NET 10).
   - `AddProblemDetails()` plus `GlobalExceptionHandler`: logs with the trace ID and returns a clean 500. Unit-tested.
   - The fallback authorization policy.
   - `TimeProvider.System`.
   - `GET /health` with `.AllowAnonymous()`.
   - Integration tests: `/health` returns 200 without sign-in, and the fallback policy requires an authenticated user.
4. **Docker:**
   - `docker/api/Dockerfile`: multi-stage, non-root, pinned images.
   - `docker/docker-compose.yml`: `api` and `sqlserver`, with a named volume.
   - `docker/.env.example`.
5. **Docs:** `CLAUDE.md` now says `Jobly.slnx`.

## Ports

Unusual ports, because the common ones are already in use on the developer's machine. Each one can be changed in `docker/.env` without touching code.

| What | Host port | Set by |
|---|---|---|
| API in Docker | 48217 → 8080 | `JOBLY_API_PORT` (compose default 48217) |
| SQL Server in Docker | 48233 → 1433 | `JOBLY_SQL_PORT` (compose default 48233) |
| API with `dotnet run` | 48219 | `src/Jobly.Api/Properties/launchSettings.json` |

`docker/.env.example` lists the variables. The developer copies it to `docker/.env` (gitignored) and sets the SQL Server password. The AI never creates or reads `.env`.

## Decisions made while preparing

- **Per-layer DI methods (`AddApplication()`, `AddInfrastructure()`) are deferred to the first feature.** A class library needs the `Microsoft.Extensions.DependencyInjection.Abstractions` package to define them, and this plan has no packages in `src/`. Both layers are empty for now, so `Program.cs` registers the few services directly. The first feature with services adds the methods and asks for that package.
- **The API container gets no connection string yet**, because nothing uses the database. It arrives with EF Core in the first feature with data.
- **Ports are published on `127.0.0.1` only.** The first run used `0.0.0.0`, which exposed SQL Server to the local network and made `http://localhost` fail on Windows (a Docker Desktop IPv6 issue). Binding to `127.0.0.1` fixes both.
- **xUnit v3 needs the Microsoft Testing Platform on .NET 10.** It's switched on in `global.json`. Single tests now run with `--filter-method` instead of `--filter`.
- **`Jobly.Application.Tests` ignores exit code 8 ("zero tests ran") for now.** The Application layer has no code yet. Remove the setting with the first service test.
- **`CS8524` is suppressed** in `Directory.Build.props`. With it suppressed, a `switch` over an enum *without* a `_` fallback compiles cleanly when every value is handled, and fails the build (`CS8509`, treated as an error) the moment a new enum value isn't handled. That makes the switch rule a compile-time check.

## Out of scope

EF Core and the `DbContext`, sign-in (and its 401 tests), OpenTelemetry and the dashboard, the React client, GitHub Actions.

## Verification

1. `dotnet build` passes with no warnings.
2. `dotnet test` passes.
3. `dotnet format --verify-no-changes` reports nothing to fix.
4. With `docker/.env` created:
   - Run `docker compose -f docker/docker-compose.yml up -d --build`.
   - `ps` shows both containers running.
   - http://localhost:48217/health shows `Healthy`. ✅ Verified 2026-10-19: API healthy, SQL Server healthy.
5. `/review-changes` is published and reviewed before any commit.
