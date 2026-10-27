# Switch to vertical slices

## Context

Jobly is built as four layered projects (Domain, Application, Infrastructure, Api). A single feature such as "Jobs" is spread over **four projects and four test projects**: the entity is in Domain, the service in Application, the repository and EF mapping in Infrastructure, the endpoints in Api. To understand or change one feature you open eight places. That is the complexity you want to remove.

In a vertical-slice layout one feature lives in **one folder**. The result is the same behaviour, the same HTTP routes, the same database, with far fewer projects, interfaces and files.

Decisions you made: **one folder per feature** (not one file per use case), **use `DbContext` directly** (no repositories), **two test projects** (unit and API).

## What it looks like

Before (today): 4 src projects, 4 test projects, 157 tests, 14 files for Jobs alone, spread over 4 projects.

After:

```
src/Jobly.Api/                       the only source project
  Program.cs
  Common/                            Result, Error, ErrorType, ResultExtensions (ToProblem), GlobalExceptionHandler
  Persistence/                       JoblyDbContext, AddPersistence(), Migrations/
  Integrations/Llm/                  ILlmClient, LlmRequest/Response, OllamaLlmClient, LlmOptions, LlmErrors, AddLlmIntegration()
  Features/
    Jobs/    Job, JobApplication, enums, JobErrors, DTOs, IJobService, JobService, JobEndpoints, EF configurations
    Cvs/     Cv, CvErrors, CvSource, DTOs, ICvService, CvService, ICvTextExtractor, CvTextExtractor, CvEndpoints, EF configuration
    Matches/ JobMatch, MatchErrors, JobMatchResponse, IMatchAgent, MatchAgent, MatchPrompt, MatchEndpoints, EF configuration
tests/
  Jobly.Api.UnitTests/               NEW. Replaces Domain.Tests, Application.Tests, Infrastructure.Tests (114 tests)
    Features/{Jobs,Cvs,Matches}/  Common/  Integrations/Llm/  Persistence/  Fakes/
  Jobly.Api.Tests/                   KEPT. HTTP endpoint tests (43 tests)
```

## How it affects the project

| Area | Change |
|---|---|
| Source projects | 4 become 1 (`Jobly.Api`). Name kept, so `dotnet run --project src/Jobly.Api`, the user-secrets id, launch settings, and the hook/permission allow-rules all keep working |
| Namespaces | `Jobly.Domain.Jobs`, `Jobly.Application.Jobs`, `Jobly.Infrastructure...` become `Jobly.Api.Features.Jobs` (and `.Cvs`, `.Matches`, `.Common`, `.Persistence`, `.Integrations.Llm`) |
| Repositories | `IJobRepository`, `ICvRepository`, `IJobMatchRepository` and the three `Sql*Repository` classes are **removed**. Services take `JoblyDbContext` and hold the queries (code moves over as is, with its comments) |
| Service interfaces | `IJobService`, `ICvService`, `IMatchAgent` stay (zero behaviour change). Interfaces remain where a real outside boundary exists: `ILlmClient`, `ICvTextExtractor` |
| HTTP API | **No change.** Same routes, same DTOs, same status codes, so the React client is untouched |
| Database | **No change.** Same tables and columns, **no new migration**. Migration files move with the project (see Hand-offs) |
| `Program.cs` | Same list of registrations, minus `AddInfrastructure`, plus `AddPersistence`. Still one readable list of features |
| Dependency rule | The compiler no longer enforces "Domain references nothing". Replaced by slice rules (below) and a test that checks them |
| Tests | 4 test projects become 2. Fake repositories (3 in Application.Tests, 3 in Api.Tests) are deleted. Service and endpoint tests run against a real in-memory SQLite database. Fakes remain for the LLM, the CV file reader, the clock, and HTTP |
| Docker | `docker/api/Dockerfile` copies one csproj instead of four. Env var names (`ConnectionStrings__Jobly`, `Integrations__Llm__*`) unchanged |
| Rules and docs | `CLAUDE.md` layout, `code-style.md`, `integrations.md`, `testing.md`, `docker.md`, `guardrails.md` text updated to the slice layout |
| Packages | Same ones, but declared in one csproj. One new test-only package (below) |

### New slice rules (replace the layer rules)

1. A feature lives in `Features/<Name>/`. Everything for it is in that folder, including its EF configuration.
2. Entities are plain C# (no EF or ASP.NET types). Services use `JoblyDbContext` but never `HttpContext`. Only `*Endpoints.cs` files use ASP.NET types.
3. `Matches` may read `Jobs` and `Cvs` (it needs both). `Jobs` and `Cvs` must not reference each other or `Matches`. A test enforces this.
4. Shared code goes in `Common/`, `Persistence/` or `Integrations/<Name>/`, never copied between slices.
5. Everything else in `.claude/rules` (Result pattern, TDD, security, logging, comments) is unchanged.

## Branch

`refactor/vertical-slices`, created from `feature/cv-and-llm` (clean working tree).

## Packages

| Package | Where | Why |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x (match the existing EF version) | test projects only | In-memory relational database for service and endpoint tests, now that repository fakes are gone. Preferred over the EF InMemory provider because it enforces unique indexes, foreign keys and cascades like the real database |

Moved, not new (same versions, declared in `Jobly.Api.csproj`): `Npgsql.EntityFrameworkCore.PostgreSQL`, `PdfPig`, `DocumentFormat.OpenXml`. The package hook will still ask once because the count in that csproj goes up.

## Database migrations

None to generate. The model is identical, so the migration `InitialPostgres` and `AddCvAndJobMatch` stay the only two. Proof: a new unit test asserts the model has no pending changes, and the existing `JoblyDbContextModelTests` script checks stay.

## Endpoints and DTOs

Unchanged. Files move into slice folders, handlers keep their `TypedResults` signatures. All current `.AllowAnonymous()` groups (marked TEMPORARY) and `/health` stay exactly as they are, so no new public endpoints.

## Hand-offs (two things only you can do, because I may not delete files or edit existing migrations)

**Before I start (about 2 minutes):** move the migrations and fix their names. I will give you the exact commands:
```
git mv src/Jobly.Infrastructure/Migrations src/Jobly.Api/Persistence/Migrations
```
then in those 5 files replace `Jobly.Infrastructure.Migrations` with `Jobly.Api.Persistence.Migrations`, `Jobly.Infrastructure.Persistence` with `Jobly.Api.Persistence`, and the entity names `Jobly.Domain.<X>.` with `Jobly.Api.Features.<X>.`. This is safe for existing databases: EF identifies a migration by its ID string (`20261023093037_InitialPostgres`), not its namespace.

**After I finish:** delete the six old project folders (`src/Jobly.Domain`, `Application`, `Infrastructure`, `tests/Jobly.Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`). They are already removed from `Jobly.slnx`, so build and test pass before you delete them.

## Execution order (after approval, no stops except the above)

1. Branch, create folders.
2. Packages: add the SQLite package, then the moved ones to `Jobly.Api.csproj` (one prompt batch).
3. Build the shared base: `Common/`, `Persistence/`, `Integrations/Llm/`.
4. Move one slice at a time, **Jobs, then Cvs, then Matches**. For each: move entity, DTOs, errors, EF configuration, service (now on `JoblyDbContext`), endpoints. Move its tests first, rewrite the service tests to use SQLite, see them go red, then green.
5. Slim `Program.cs`, update `Jobly.slnx`, both test csprojs, `InternalsVisibleTo`, Dockerfile.
6. Add the two safety tests: "no pending model changes" and "slices only reference allowed slices".
7. `dotnet build`, `dotnet test`, `dotnet format`. Test count must stay at least 157 (plus the 2 new).
8. Update `CLAUDE.md` and the rules files (these are guardrail files, so each edit shows an approval prompt).
9. `/review-changes`, then one report back. Commit is yours.

## Tests

- All 157 existing tests are kept with the same intent. Never weakened: only the arrange step changes (adding rows to a test database instead of a fake list).
- New: `Model_HasNoPendingChanges` and `Slices_OnlyReferenceAllowedSlices`.
- Reused: `FakeLlmClient`, `FakeCvTextExtractor`, `FixedTimeProvider`, `FakeHttpMessageHandler`.
- New helper: `TestDatabase` (opens an in-memory SQLite connection, creates the schema, returns a `JoblyDbContext`) in `Jobly.Api.UnitTests`. `JoblyApiFactory` gets the same treatment and a `Seed(...)` method for endpoint tests.

## Assumptions (change any of these now)

1. Single project keeps the name `Jobly.Api` (no rename).
2. Folder names stay `Jobs`, `Cvs`, `Matches` (route stays `/api/cv`).
3. Service interfaces are kept for now. Dropping them later is a small separate step.
4. Test projects are `Jobly.Api.UnitTests` (new) and `Jobly.Api.Tests` (kept), so the `<Project>.Tests` naming rule still reads naturally. `Jobly.Api.Tests` keeps its own copy of the two small fakes rather than referencing another test project.
5. `Jobly.Api` gets `InternalsVisibleTo` for both test projects.
6. Old plan files in `docs/plans/` are history and are not rewritten.

## Risks

- **SQLite is not PostgreSQL.** SQLite cannot sort by `DateTimeOffset` directly (`ListJobs` orders by `CreatedAt`). Fix: `TestDatabase` stores `DateTimeOffset` as a number, in test code only. If this proves fragile, the fallback is real PostgreSQL via Testcontainers, which needs Docker running during tests and one more package, so I would stop and ask.
- **Pending-model-changes API.** The test uses EF's `HasPendingModelChanges()`. If it is unavailable in this EF version, I fall back to comparing the generated create-script, which the existing model tests already do.
- **The compiler no longer enforces layers.** Mitigated by the slice-reference test.

## Verification

1. `dotnet build` and `dotnet test`: green, at least 159 tests.
2. `dotnet format --verify-no-changes`.
3. `node .claude/hooks/test-guards.js`: still 91/91.
4. `docker compose -f docker/docker-compose.yml up -d --build`: API starts with one csproj; `GET /health` is 200; `GET /api/jobs` returns the same JSON as before; the client still loads jobs, saves a CV and shows a match.
5. `git diff --stat` shows only moves, namespace changes, repository removal, and the test setup change; no schema change.

## In short (Roman Nepali)

Abhi ek feature (jastai Jobs) ko code 4 ota project ma bikhirinchha. Naya tarika ma ek feature ko sabai kura (`Job` entity, service, endpoint, EF config) ek folder `Features/Jobs/` ma hunchha, ra 4 project ek `Jobly.Api` project ma milchha. `Repository` hataera service le seedhai `JoblyDbContext` use garchha, tesaile file kam hunchha. API routes ra database bilkul same rahanchha, tesaile React client ra migration naya banaunu parˈdaina. Tapai le 2 kaam garnu parchha: suru ma migrations folder `git mv` garne, ra ant ma purana 6 folder delete garne.
