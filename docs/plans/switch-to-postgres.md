# Switch the database from SQL Server to PostgreSQL (with pgvector ready)

## Context

Jobly stores its data in SQL Server today (EF Core, Docker, one `InitialJobs` migration). You decided to move to **PostgreSQL**, because the next features (CV + LLM + embeddings) need to store and search **vectors**, and PostgreSQL does that natively with the `pgvector` extension (a `vector` column, distance operators, and indexes). SQL Server 2022 cannot do that.

This plan is only the **swap**: same tables, same endpoints, same behavior. The vector columns, embeddings, the CV and the Match agent come in the **next plan** (draft at the end of this file).

Why now: it is cheap. There is one migration, two entities, no deployed database, and no real data (the smoke-test job was deleted).

Decisions:
- **The whole app moves to PostgreSQL.** One database, not two.
- The Docker image is the **`pgvector/pgvector`** image (PostgreSQL with the extension installed but not enabled). The extension is enabled in the next plan's migration, together with the first vector column.
- The old SQL Server migration cannot be used with PostgreSQL. It only ever ran on your local Docker volume, never on a shared database, so replacing it with a new initial migration is safe. **You delete the old migration files** (I am not allowed to delete), and I generate a new one.

Branch: `feature/switch-to-postgres`, created from `feature/job-tracker-crud` (the code it changes is not in `main` yet).

## What changes

### Infrastructure (`Jobly.Infrastructure/`)
- `Jobly.Infrastructure.csproj` and `Directory.Packages.props`: replace `Microsoft.EntityFrameworkCore.SqlServer` with **`Npgsql.EntityFrameworkCore.PostgreSQL`** (the EF Core 10 line). `Microsoft.EntityFrameworkCore.Design` stays. If the build passes without it, the `System.Security.Cryptography.Pkcs` pin is removed (it only existed to fix a package that SqlClient brought in).
- `DependencyInjection.cs`: `UseSqlServer` becomes `UseNpgsql`. Same `ConnectionStrings:Jobly` setting.
- `JobConfiguration`: remove the `HasFilter` on the `(Source, ExternalId)` index. It is SQL Server syntax, and PostgreSQL does not need it, because a unique index there already treats several NULLs as different. Everything else (lengths, enum-as-text, one-to-one, cascade, the status index) maps the same. Comments updated.
- Dates: PostgreSQL stores `DateTimeOffset` as `timestamp with time zone` and only accepts UTC values. All our times come from `TimeProvider.GetUtcNow()`, so they already are UTC. A test covers it.
- Migrations: **you delete** the folder `src/Jobly.Infrastructure/Migrations/` (3 files). Then I run `dotnet ef migrations add InitialPostgres` (new, with PostgreSQL types: `uuid`, `varchar`/`text`, `timestamptz`).

### New test project `tests/Jobly.Infrastructure.Tests` (added to `Jobly.slnx`)
No database needed. It builds `JoblyDbContext` with `UseNpgsql` and a dummy connection string, then checks the model and `Database.GenerateCreateScript()`:
- both tables exist, with `uuid` and `timestamp with time zone` columns,
- the unique indexes exist (`JobId`, and `Source + ExternalId`), and the status index exists,
- the script has no SQL Server syntax (square brackets, `nvarchar`),
- a `DateTimeOffset` that is not UTC is not silently accepted.

The same project is reused by the next plan for the LLM client and the CV text extractor tests.

### Api tests
`JoblyApiFactory`: the dummy connection string changes to the PostgreSQL form (`Host=not-used;Database=not-used`). It is never connected, because the repository is faked. No other test changes.

### Docker (`docker/`)
- `docker-compose.yml`: the `sqlserver` service becomes `postgres`:
  - pinned `pgvector/pgvector` image (a specific PostgreSQL 17 tag, checked when it is pulled),
  - `POSTGRES_USER` and `POSTGRES_DB` fixed, `POSTGRES_PASSWORD` from `docker/.env` with the same "missing value" stop message as today,
  - a named volume `postgres-data` and a health check with `pg_isready`,
  - port `127.0.0.1:${JOBLY_DB_PORT:-48233}:5432` (same host port as before, your machine only),
  - the `api` service waits until it is healthy and keeps `ConnectionStrings__Jobly` from `JOBLY_CONNECTION_STRING`.
- `docker/.env.example`: `SQL_SA_PASSWORD` becomes `POSTGRES_PASSWORD`, `JOBLY_SQL_PORT` becomes `JOBLY_DB_PORT`, and `JOBLY_CONNECTION_STRING` shows the PostgreSQL format (`Host`, `Port`, `Database`, `Username`, and the password written as a `{placeholder}`).
- The old `sqlserver` container and `sqlserver-data` volume stay on your machine, unused (I cannot remove containers or volumes). You can remove them yourself when you want. Compose may print an "orphan container" warning until then.

### Guardrails and docs (guardrail files ask for your approval)
- `.claude/hooks/guard-shell.js`: add `psql`, `pg_dump`, `pg_restore`, `createdb`, `dropdb` next to `sqlcmd` in the "database access needs approval" list, with cases in `test-guards.js`.
- `.claude/settings.json`: an ask rule for `psql` next to `sqlcmd` (Bash and PowerShell).
- Text updates from "SQL Server" to "PostgreSQL": `CLAUDE.md`, `.claude/rules/docker.md`, `.claude/rules/guardrails.md`, and `integrations.md` / `logging.md` where they name it.
- `docs/plans/solution-setup.md` is history and stays as it is.

## Tests (written first)

1. The `Jobly.Infrastructure.Tests` model tests above. They fail first (the index filter is SQL Server only), then pass after the configuration fix.
2. `dotnet test` for everything else stays green and unchanged (137 tests today, plus the new ones).
3. The end-to-end check against real PostgreSQL in Docker (below). The repository itself has no unit tests, same as before, so this is where it is proven.

## Packages and approvals (need you)

- **NuGet:** add `Npgsql.EntityFrameworkCore.PostgreSQL`, remove `Microsoft.EntityFrameworkCore.SqlServer`. No other package.
- **You do (I cannot):**
  1. Delete the 3 files in `src/Jobly.Infrastructure/Migrations/`.
  2. Edit `docker/.env`: set `POSTGRES_PASSWORD`, optionally `JOBLY_DB_PORT`, and the new `JOBLY_CONNECTION_STRING` (same password in both places).
  3. Update the user secret `ConnectionStrings:Jobly` to the host form: host `127.0.0.1` (not `localhost`, as before), port `48233`, database `jobly`, user `jobly`, and your password.
- **You approve:** the NuGet change, `dotnet ef migrations add InitialPostgres`, `dotnet ef database update`, and the guardrail file edits.

## Build order (small steps, a short report after each)

1. Branch, the new `Jobly.Infrastructure.Tests` project, the NuGet swap, and the failing model test
2. Fix the configuration and switch to `UseNpgsql`; tests green; drop the Pkcs pin if possible
3. Docker: compose, `.env.example`, and the `JoblyApiFactory` string
4. Guardrail hook, settings and text updates, plus the hook self-test
5. You: delete the old migrations and update `.env` and the user secret. Me: create the new migration
6. `docker compose up`, apply the migration, run the same API smoke test as before, and confirm the `vector` extension is available (one approved `psql` check)
7. `/review-changes`, then small commits and push

## Verification

1. `dotnet build` is clean, `dotnet test` is green, `dotnet format` is clean, and `node .claude/hooks/test-guards.js` passes.
2. `docker compose -f docker/docker-compose.yml up -d --build`: `postgres` becomes healthy and the API answers `/health`.
3. `dotnet ef database update` creates the `jobly` database with the `Jobs` and `JobApplications` tables.
4. Smoke test: create, list, get, edit, notes, every status move, 409 for a disallowed move, 400, 404, filter by status, delete. Same results as on SQL Server.
5. The board at `http://127.0.0.1:48251` still works: add a job, drag a card, reload, delete.
6. A query on `pg_available_extensions` for the name `vector` returns a row (pgvector is ready for the next plan).

---

## Next plan (draft, not part of this approval): CV + LLM + Match agent + embeddings on PostgreSQL

The existing draft `docs/plans/cv-and-llm.md` (CV + LLM + Match agent) stays the base. It will be rewritten for PostgreSQL after this swap. What changes in it:

- **Embeddings added.** `IEmbeddingClient` next to `ILlmClient` in `Integrations/Llm/`, using an embedding model on the same local Ollama. Vectors are stored in PostgreSQL `vector(N)` columns (NuGet `Pgvector.EntityFrameworkCore`, and a migration that enables the `vector` extension), with an HNSW index and the similarity search done in SQL (`ORDER BY embedding <=> query`).
- **What embeddings are used for (decided in that plan):** a quick similarity badge on each card (no waiting), "best match first" sorting, and picking only the most relevant CV chunks for the Match agent, so a small local model gets a short, focused prompt (RAG).
- **Persistence names:** `SqlCvRepository` and similar become Npgsql-based repositories; the new migration is `AddCvAndJobMatch` on top of `InitialPostgres`.
- Everything else already agreed stays: PDF or DOCX upload (paste as a fallback), local LLM first (Ollama on the host, provider-neutral `ILlmClient`), Match agent (score, matching skills, gaps), one CV for the whole app until the auth plan, cover letter agent after that.
- Approvals then: NuGet `UglyToad.PdfPig`, `DocumentFormat.OpenXml`, `Pgvector.EntityFrameworkCore`; a `dotnet ef` migration; you install Ollama and pull a chat model and an embedding model.
