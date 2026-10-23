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
  Jobly.Domain/          Entities, value objects, domain rules, Result/Error types (Common/). References nothing.
  Jobly.Application/     Use-case services, DTOs, repository interfaces, integration contracts (Integrations/<Name>/). References Domain only.
  Jobly.Infrastructure/  EF Core DbContext, migrations, repositories, integration implementations (Integrations/<Name>/). References Application and Domain.
  Jobly.Api/             Minimal API endpoints (Endpoints/<Feature>/), validation, Result→HTTP mapping, auth, observability, DI composition root.
client/                  React front end
docker/                  docker-compose.yml plus one folder per service (api/, client/, postgres/, observability/)
tests/
  Jobly.Domain.Tests/
  Jobly.Application.Tests/   Unit tests for every service method
  Jobly.Api.Tests/           Integration tests, at least one per endpoint
docs/
  plans/                 One feature plan per file: docs/plans/<feature-name>.md
.claude/
  settings.json          Permission lists and hook registration
  hooks/                 Guardrail scripts that run before every command and file change
  rules/                 Guardrails, workflow, code style, testing (loaded automatically)
```

Dependencies point inward (Api/Infrastructure → Application → Domain). Application declares the interfaces it needs and Infrastructure implements them. The two are wired together only in `Jobly.Api` through dependency injection. Code in Domain or Application must never reference EF Core or ASP.NET Core types.

External services, including the LLM, are **integrations**. Each one has its contract in `Jobly.Application/Integrations/<Name>/` and its whole implementation (options, typed HttpClient, errors, DI extension) in `Jobly.Infrastructure/Integrations/<Name>/`. Expected failures flow as `Result<T>` instead of exceptions. Every request carries a trace ID through `ILogger`, HttpClient, and EF Core for observability.

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
```

Subagents work in git worktrees under `.claude/worktrees/`. The guardrails must be committed first, because a worktree only contains committed files. See [workflow.md](.claude/rules/workflow.md).

`/review-changes [base]` builds an explained, side-by-side review page of the branch and publishes it as a private Artifact. Run it before asking to commit or push. The skill lives in `.claude/skills/review-changes/`, and generated pages go to the gitignored `.reviews/` folder.

Plan-mode plans are saved to `docs/plans/` (`plansDirectory` in `.claude/settings.json`). Rename a plan to `<feature-name>.md` once it's approved.

These need approval first:

```bash
dotnet ef migrations add <MigrationName> --project src/Jobly.Infrastructure --startup-project src/Jobly.Api
dotnet ef database update --project src/Jobly.Infrastructure --startup-project src/Jobly.Api
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
- [docker.md](.claude/rules/docker.md): one folder per service under `docker/`
- [security.md](.claude/rules/security.md): access declared per route group, protected by default, ownership checks

Every rule states its **Why**. Keep it that way when adding rules.

The most important ones:

- Plan in `docs/plans/<feature-name>.md` and wait for approval before coding.
- Once a plan is approved, files for that feature are created and edited inside the project without asking per file. Non-read-only commands, network access, SQL access, packages, and guardrail changes still need approval. Deleting is never allowed.
- Every route group declares its access (`.RequireAuthorization()`), endpoints override only where they differ, and the fallback policy closes anything unmarked.
- A task is done only when `dotnet build` and `dotnet test` pass.
- Explain every change, and comment anything that could confuse a junior developer.
- If a guardrail hook blocks something, stop and explain. Never work around it.
