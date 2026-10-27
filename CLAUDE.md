# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Jobly is an ASP.NET Core Web API (.NET 10) with a React front end. PostgreSQL (with the pgvector extension for vectors) is accessed through Entity Framework Core, backend tests use xUnit, and it ships with Docker, GitHub Actions, and Azure App Service.

Docker Compose is the main way to run the app locally (API, client, PostgreSQL, dashboard). `dotnet build` and `dotnet test` run directly on the host for fast TDD feedback.

The repository is at an early stage. The layout below is the agreed target. Where it differs from what's on disk, follow the target and say so.

## Layout

```
Jobly.slnx               Solution (XML format). Shared settings: Directory.Build.props, Directory.Packages.props, global.json
src/
  Jobly.Api/             The one source project, organised in vertical slices.
    Program.cs           Composition root: DI registrations and the request pipeline, one readable list of features.
    Features/<Name>/     One folder per feature (Jobs, Cvs, Matches): entity, errors, DTOs, service, endpoints and EF configuration together.
    Common/              Result/Error types, Result→HTTP mapping (ToProblem), global exception handler. Shared by every feature.
    Persistence/         JoblyDbContext, AddPersistence(), Migrations/.
    Integrations/<Name>/ An external service (Llm/): contract, options, typed HttpClient, errors and DI extension in one folder.
client/                  React front end
docker/                  docker-compose.yml plus one folder per service (api/, client/, postgres/, observability/)
tests/
  Jobly.Api.UnitTests/   Entity tests, service tests (real in-memory SQLite database), CV reader, LLM client, EF model, slice rules. Mirrors src/ folders.
  Jobly.Api.Tests/       Endpoint integration tests (WebApplicationFactory), at least one per endpoint
docs/
  plans/                 One feature plan per file: docs/plans/<feature-name>.md
.claude/
  settings.json          Permission lists and hook registration
  hooks/                 Guardrail scripts that run before every command and file change
  rules/                 Guardrails, workflow, code style, testing (loaded automatically)
```

**Slice rules** (one feature = one folder, so you read one place to understand it):

1. A feature lives in `Features/<Name>/`, including its EF configuration (`IEntityTypeConfiguration`).
2. Entities are plain C# with no EF Core or ASP.NET Core types. Services use `JoblyDbContext` directly (no repository layer) but never `HttpContext`. Only `*Endpoints.cs` files use ASP.NET Core types.
3. `Matches` may use `Jobs` and `Cvs` (it compares a job with the CV). `Jobs` and `Cvs` must not use each other or `Matches`. The test `SliceRulesTests` enforces this.
4. Shared code goes in `Common/`, `Persistence/` or `Integrations/<Name>/`, never copied between slices.

External services, including the LLM, are **integrations**: everything about one (contract, options, typed HttpClient, errors, DI extension) is in `Integrations/<Name>/`. Features depend on the interface (`ILlmClient`), never on the provider class. Expected failures flow as `Result<T>` instead of exceptions. Every request carries a trace ID through `ILogger`, HttpClient, and EF Core for observability.

## Commands

You may run these without asking:

```bash
dotnet build
dotnet test
dotnet test --project tests/<Project> --filter-method "<Namespace>.<Class>.<Method>"   # single test (wildcards: "*Method*")
dotnet format
dotnet run --project src/Jobly.Api
docker compose -f docker/docker-compose.yml up -d --build                # start or rebuild the whole stack
docker compose -f docker/docker-compose.yml logs api                     # logs of one service
docker compose -f docker/docker-compose.yml down                         # stop the stack (never with -v)
node .claude/hooks/test-guards.js                                        # self-test the guardrail hooks
git switch -c feature/<name>                                             # create a branch (also: git checkout -b <name>)
mkdir <folder>                                                           # folders inside the project
dotnet ef migrations add <MigrationName> --project src/Jobly.Api   # generates files only
```

Subagents work in git worktrees under `.claude/worktrees/`. The guardrails must be committed first, because a worktree only contains committed files. See [workflow.md](.claude/rules/workflow.md).

`/review-changes [base]` builds an explained, side-by-side review page of the branch and publishes it as a private Artifact. Run it before asking to commit or push. The skill lives in `.claude/skills/review-changes/`, and generated pages go to the gitignored `.reviews/` folder.

Plan-mode plans are saved to `docs/plans/` (`plansDirectory` in `.claude/settings.json`). Rename a plan to `<feature-name>.md` once it's approved.

These need approval first:

```bash
dotnet ef database update --project src/Jobly.Api
npm install          # in client/
npm run dev          # in client/
```

## Rules

The rules are in `.claude/rules/`:

- [guardrails.md](.claude/rules/guardrails.md): what is blocked, what needs approval, and how each rule is enforced
- [workflow.md](.claude/rules/workflow.md): plan first, TDD, definition of done, git, and how to explain changes
- [code-style.md](.claude/rules/code-style.md): C# conventions (Result pattern, async, `TimeProvider`, `IHttpClientFactory`, comments)
- [testing.md](.claude/rules/testing.md): xUnit naming and structure
- [logging.md](.claude/rules/logging.md): structured logging, trace IDs, LLM telemetry, dashboard
- [integrations.md](.claude/rules/integrations.md): one folder per external integration, loosely coupled
- [architecture.md](.claude/rules/architecture.md): vertical slices, what a feature folder holds, and the slice rules
- [docker.md](.claude/rules/docker.md): one folder per service under `docker/`
- [security.md](.claude/rules/security.md): access declared per route group, protected by default, ownership checks

Every rule states its **Why**. Keep it that way when adding rules.

The most important ones:

- Plan in `docs/plans/<feature-name>.md` as an approval manifest (branch, directories and files, packages, planned database migrations, endpoints, tests, assumptions) and wait for approval before coding. Ask all open questions while planning.
- Once the plan is approved, build the whole feature without stopping: create the branch and folders, install the listed packages first in one batch, work test first, generate the migration, run build, test and format, run `/review-changes`, then report back once. Stop only for a guardrail prompt, something outside the plan, a contradictory requirement, or a failing test that needs a real fix. See [workflow.md](.claude/rules/workflow.md).
- Files for the feature are created and edited inside the project without asking per file. Packages, network access, SQL access, `dotnet ef database …`, commits, and guardrail changes still need approval. Deleting is never allowed.
- Every route group declares its access (`.RequireAuthorization()`), endpoints override only where they differ, and the fallback policy closes anything unmarked.
- A task is done only when `dotnet build` and `dotnet test` pass.
- Explain every change, and comment anything that could confuse a junior developer.
- If a guardrail hook blocks something, stop and explain. Never work around it.
