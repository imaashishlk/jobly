# Job tracker CRUD (API + React Kanban)

## Context

Jobly's first real feature: save jobs and track them on a Kanban board. A job comes from Arbeidsplassen or from anywhere else (the user pastes the description). Statuses: **Saved, Planning to apply, Applied, Interview, Offer, Rejected, Withdrawn**.

Later plans build AI agents on top (CV vs job match, cover letters, ...). This plan is CRUD only, with **no authentication** yet.

Decisions already made:
- Scope: API **and** React Kanban board.
- Arbeidsplassen import: **later, own plan**. This plan only stores `Source` and optional `ExternalId`, so the import can plug in without a schema change.
- Model: `Job` (the posting) + `JobApplication` (your tracking of it, with status).
- No auth now. Nothing in this plan stops us adding it later.

Rename this file to `docs/plans/job-tracker-crud.md` after approval. Branch: `feature/job-tracker-crud`.

## Domain (`Jobly.Domain/Jobs/`)

- `Job`: `Id` (Guid), `Title`, `Company`, `Location?`, `Description`, `Url?`, `Source` (`Manual` | `Arbeidsplassen`), `ExternalId?`, `CreatedAt`.
- `JobApplication`: `Id`, `JobId`, `Status`, `Notes?`, `AppliedAt?`, `CreatedAt`, `UpdatedAt`. One per job (1:1 for now).
- `ApplicationStatus` enum: `Saved, PlanningToApply, Applied, Interview, Offer, Rejected, Withdrawn`.
- Factory `Job.Create(...)` returns `Result<Job>` and also creates the application in `Saved`. Rules: title, company, and description are required (trimmed, not empty).
- `JobApplication.ChangeStatus(status, now)` returns a `Result` and only allows these moves (a `switch` expression over the current status, no `_` fallback):
  - Saved -> PlanningToApply, Applied, Withdrawn
  - PlanningToApply -> Applied, Withdrawn
  - Applied -> Interview, Rejected, Withdrawn, Ghosted
  - Interview -> Offer, Rejected, Withdrawn, Ghosted
  - Offer, Rejected, Withdrawn, Ghosted -> nothing (final)
  
  (Ghosted = no reply after applying or after an interview. Added later; the plan's "7 columns" is now 8.)
  
  Any other move returns `Job.InvalidStatusChange` (type `Conflict`, so the API answers 409). Moving to the same status is also invalid. `AppliedAt` is set when status becomes `Applied`. Because there is no way back, that happens only once.
- `JobErrors`: `Job.NotFound`, `Job.TitleRequired`, ... (same style as `Error` in `Jobly.Domain/Common/Error.cs`). Private setters and `protected`/`private` constructors so an invalid entity can't be built.

## Application (`Jobly.Application/Jobs/`)

- `IJobRepository`: `GetAll(status?)`, `GetById`, `Add`, `Update`, `Delete`, all with `CancellationToken`.
- `IJobService` / `JobService` (inject `IJobRepository` and `TimeProvider`): `ListJobs`, `GetJob`, `CreateJob`, `UpdateJob`, `ChangeStatus`, `UpdateNotes`, `DeleteJob`. All return `Result<T>`.
- DTOs: `CreateJobRequest`, `UpdateJobRequest`, `ChangeStatusRequest`, `UpdateNotesRequest`, `JobResponse` (job fields + status, notes, appliedAt). Data annotations (`[Required]`, `[MaxLength]`, `[Url]`) on requests. `Description` max about 20,000 characters (job ads can be long).
- DI extension `AddJobsFeature()` registers the service.

## Infrastructure (`Jobly.Infrastructure/`)

- `JoblyDbContext` with `Jobs` and `JobApplications`. Entity configuration in `Persistence/Configurations/` (max lengths, status stored as string so the DB is readable, unique index on `JobApplication.JobId`, unique filtered index on `(Source, ExternalId)` to stop duplicate imports later).
- `SqlJobRepository : IJobRepository`. Queries use `.Select(...)` projections or `AsNoTracking()` for reads.
- `AddInfrastructure(configuration)`: registers DbContext against SQL Server and the repository. Connection string comes from env var / user secrets (already wired in docker compose).
- **Migration** `InitialJobs`. Needs your approval to run `dotnet ef migrations add`.
- Apply migrations: manually via `dotnet ef database update` (needs approval) for now. No auto-migrate on startup.

## Api (`Jobly.Api/Endpoints/Jobs/`)

`JobEndpoints.MapJobEndpoints()` called from `Program.cs`. Group `/api/jobs`, handlers return `TypedResults` with `Results<...>` and use `ToProblem()` from `Common/ResultExtensions.cs`.

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/jobs?status=` | List (Kanban data), optional status filter | 200 |
| GET | `/api/jobs/{id}` | One job | 200, 404 |
| POST | `/api/jobs` | Save a job (starts as `Saved`) | 201, 400 |
| PUT | `/api/jobs/{id}` | Edit job fields | 200, 400, 404 |
| PUT | `/api/jobs/{id}/status` | Move card to another column (allowed moves only) | 200, 400, 404, 409 |
| PUT | `/api/jobs/{id}/notes` | Edit notes | 200, 400, 404 |
| DELETE | `/api/jobs/{id}` | Remove job and its tracking | 204, 404 |

**Access (temporary, must be listed per security.md):** the whole `/api/jobs` group is `.AllowAnonymous()` with a comment "TEMPORARY: no authentication yet, replaced by `.RequireAuthorization()` in the auth plan." Without it the fallback policy would return 401 for everything. No ownership checks yet: the auth plan adds `UserId` to `Job`, an `ICurrentUser`, ownership checks, and the `401/403` tests.

Also: `AddJobsFeature()` and `AddInfrastructure()` calls in `Program.cs`; log statements (with the required comment) on create, status change, delete.

## React client (`client/`)

- Vite + React + **TypeScript**. Dev server proxies `/api` to the API so no CORS setup is needed. In Docker, nginx proxies `/api` to the `api` service for the same reason.
- Pages/components: `BoardPage` (7 columns, one per status, cards show title, company, source), `JobCard`, `JobFormDialog` (add/edit: title, company, location, URL, description paste box), `JobDetailPanel` (full description, notes, delete).
- Moving a card: **native HTML5 drag-and-drop** (no drag library), plus a status dropdown on the card as a fallback for keyboard/touch. The client holds the same allowed-moves table, so while dragging only the valid columns light up, and cards in a final column (Offer, Rejected, Withdrawn) can't be dragged. The API is still the authority: it returns 409 for an invalid move. The UI updates at once and rolls back if the call fails.
- **Look and feel:** professional and interactive, with colors inspired by Claude's website (warm, not a copy of its layout or UI). Design tokens as CSS variables in one `theme.css`, with a light and a dark set:
  - Background warm cream `#FAF9F5`, cards white-ish `#FFFFFF` with a soft border `#E8E6DC`, text near-black `#141413`, muted text `#6B6A63`.
  - Accent terracotta `#D97757` (hover `#C15F3C`) for primary buttons, focus rings, and the active column.
  - Status colors, muted and warm so the board stays calm: Saved gray, Planning blue-gray, Applied terracotta, Interview amber, Offer green, Rejected soft red, Withdrawn light gray.
  - Serif font for headings and a clean sans for UI text, both system fonts (no font package).
  - Interactions: cards lift on hover, a drop-zone highlight while dragging, smooth 150 to 200 ms transitions, toast messages for success and errors, empty-state hints in each column, keyboard focus styles. Respects `prefers-reduced-motion`.
- A small `api.ts` wrapper over `fetch` that reads `ProblemDetails` (`detail`, `errorCode`, `traceId`) and shows the message.
- Docker: `docker/client/Dockerfile` (node build stage, nginx runtime, non-root, pinned versions), `docker/client/nginx.conf`, a `client` service in `docker/docker-compose.yml` on `127.0.0.1` with the port in `docker/.env.example`.
- Client tests: none for now (no test package approved). Verified by running the stack.

## Tests (TDD, written first)

- `Jobly.Domain.Tests`: `Job.Create` valid and invalid inputs; `ChangeStatus` for every allowed move, every disallowed move (a Theory over the whole table, including same-status and moves out of final states), and `AppliedAt` set on `Applied`.
- `Jobly.Application.Tests`: every `JobService` method, success and failure paths, using hand-written `Fakes/FakeJobRepository` and `FixedTimeProvider`.
- `Jobly.Api.Tests`: at least one integration test per endpoint via `WebApplicationFactory`, with `FakeJobRepository` swapped in (no real DB). Includes a 400 validation case and a 404 case.

## Packages (need your approval)

- NuGet (in `Directory.Packages.props`): `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` (for migrations). Version matches the installed .NET 10 runtime.
- npm (`client/`): `react`, `react-dom`, `vite`, `@vitejs/plugin-react`, `typescript`, `@types/react`, `@types/react-dom`. No UI kit, no drag-and-drop library, no state library.

## How I'll work

Small steps. After each step I report what I did in short bullet points. Code stays simple and readable, with no extra patterns beyond this plan. I ask before any big decision not covered here. At the end I run all tests and show you the result.

## Build order (each step: failing test, then code, `dotnet build` + `dotnet test`)

1. Domain + tests
2. Application + tests
3. Infrastructure (DbContext, repository, migration) 
4. API endpoints + integration tests
5. Docker: run the API against SQL Server, smoke test with HTTP requests
6. React board, Docker client service, test in the browser

## Approval points

Every file edit (hook), NuGet and npm packages, `dotnet ef migrations add` and `database update`, `npm install`/`npm run dev`, commits, and the `/review-changes` page before commit.

## Verification

1. `dotnet build` and `dotnet test` pass; `dotnet format` is clean.
2. `docker compose -f docker/docker-compose.yml up -d --build`, apply the migration, then create a job, move it through all seven statuses, edit notes, and delete it with HTTP requests against the API.
3. Open the board in the browser: add a job by pasting a description, drag it across columns, reload the page and see the status persisted, delete it.
4. Error check: empty title returns 400 with `errorCode`, unknown id returns 404, both include `traceId`.

## Roadmap, not in this plan

- **Auth plan:** JWT or Entra ID, `UserId` on jobs, `ICurrentUser`, ownership checks, `401/403` tests, the group switches to `.RequireAuthorization()`.
- **Arbeidsplassen integration:** `Integrations/Arbeidsplassen/` contract in Application, implementation in Infrastructure. Search and import into `Job` with `Source = Arbeidsplassen`.
- **CV storage:** upload/paste the CV (plain text first, parsed text stored once).
- **LLM integration:** `ILlmClient` in `Integrations/Llm/` with telemetry (tokens, latency, no content logging).
- **Agents**, each a service on `ILlmClient`, results stored against the `JobApplication`:
  1. **Match agent:** CV vs job description, gives a score, matching skills, gaps.
  2. **Cover letter agent:** writes a letter from CV + job + match gaps.
  3. **Job parser agent (suggested first):** turns a pasted ad into title, company, location, requirements, so adding a job is one paste.
  4. **CV tailoring agent:** suggests which CV points to emphasize for this job.
  5. **Interview prep agent:** likely questions and answers based on the job and your CV (runs when a card reaches `Interview`).
  6. **Follow-up agent:** drafts follow-up or thank-you emails when a card sits in `Applied` too long.
