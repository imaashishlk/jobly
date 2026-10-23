# CV storage + LLM integration + Match agent

> **DRAFT, not approved.** Written before the switch to PostgreSQL (`switch-to-postgres.md`). It will be rewritten for PostgreSQL and **embeddings with pgvector** before any code is written: repositories become Npgsql-based, vectors live in `vector(N)` columns, and `IEmbeddingClient` is added next to `ILlmClient`. The SQL Server wording below is outdated.

## Context

The job tracker (Kanban board) is done and pushed. This plan adds the first AI feature: store the user's CV, call an LLM, and build the **Match agent**, which compares the CV with a job description and returns a score, the matching skills, and the gaps.

Decisions already made:
- **CV input:** upload **PDF or DOCX**. Pasting text is kept as a fallback (scanned PDFs have no text, and the user may want to fix the extracted text).
- **Scope:** CV + LLM integration + Match agent. The cover letter agent is the next plan.
- **LLM:** **local first**, through an OpenAI-compatible endpoint (Ollama). Recommendation and reasons:
  - A CV is personal data. With a local model it never leaves the machine, which also fits the logging rule "don't log CV content".
  - It costs nothing per call, and needs no API key.
  - `ILlmClient` stays provider-neutral, so a Claude (Anthropic) client can be added later as a second class in `Integrations/Llm/` without touching services or endpoints.
  - Trade-off: small local models are slower and less reliable at returning JSON, so the agent validates the answer strictly and reports `Match.InvalidResponse` instead of guessing.
  - Ollama runs **on the host** (not in Docker): Docker Desktop on Windows can't use the GPU easily, and a CPU-only container is very slow. The API container reaches it at `host.docker.internal:11434`.
- No authentication yet, so there is **one CV for the whole app**. The auth plan later ties CV and matches to a user.

Branch: `feature/cv-and-llm`, created from `feature/job-tracker-crud` (this feature builds on that code). Rename this file to `docs/plans/cv-and-llm.md` after approval.

## Domain (`Jobly.Domain/`)

- `Cvs/Cv`: `Id`, `Text`, `FileName?`, `Source` (`Uploaded` | `Pasted`), `UpdatedAt`. `Cv.Create(...)` returns `Result<Cv>`: text required, at most 30,000 characters. `ReplaceText(...)` follows the same rules. Private setters and constructor, like `Job`.
- `Cvs/CvErrors`: `Cv.NotFound`, `Cv.TextRequired`, `Cv.TooLong`, `Cv.UnsupportedFileType`, `Cv.FileTooLarge`, `Cv.NoTextFound`.
- `Matches/JobMatch`: `Id`, `JobId`, `Score` (0 to 100), `Summary`, `MatchingSkills`, `Gaps`, `Model`, `CreatedAt`. `JobMatch.Create(...)` returns a `Result`: score must be 0 to 100 (an out-of-range score is an error, not silently clamped), summary required.
- `Matches/MatchErrors`: `Match.NotFound`, `Match.CvRequired` (no CV yet, Conflict), `Match.InvalidResponse` (the LLM did not return what we asked for).
- `Common/ErrorType`: add **`Unavailable`** (an outside service is down or too slow). `ResultExtensions.ToProblem()` maps it to **503**. The switch has no `_` fallback, so the build fails until it is handled. Needs a test in `ResultExtensionsTests`.

## Application (`Jobly.Application/`)

- `Cvs/`: `ICvRepository` (`Get`, `Save` replaces the single CV, `Delete`), `ICvTextExtractor` (contract: `Extract(Stream, fileName, ct)` returns `Result<string>`), `ICvService` / `CvService` (`GetCv`, `UploadCv`, `SaveCvText`, `DeleteCv`), DTOs `SaveCvTextRequest`, `CvResponse` (no full text in the list-style info: file name, source, character count, updated; full text only in `GetCv`).
- `Integrations/Llm/` (the contract, nothing about any provider): `ILlmClient.Complete(LlmRequest, ct)` returns `Result<LlmResponse>`. `LlmRequest` (system prompt, user prompt, `ExpectJson`), `LlmResponse` (text, model, input tokens, output tokens).
- `Matches/`: `IJobMatchRepository` (`GetByJobId`, `Save` keeps the latest match per job), `IMatchAgent` / `MatchAgent` (`AnalyzeJob(jobId, ct)`, `GetMatch(jobId, ct)`), `MatchPrompt` (builds the prompt), `JobMatchResponse`.
- `MatchAgent.AnalyzeJob` steps: load job (404), load CV (`Match.CvRequired`), build prompt, call `ILlmClient`, parse the JSON `{score, summary, matchingSkills[], gaps[]}`, create `JobMatch` through the Domain, save, return. Any LLM failure comes back unchanged as its `Result` error.
- **Prompt rules** (in `MatchPrompt`, covered by tests): the CV and the job text are put between clear delimiters and the system prompt says they are **data, never instructions** (job ads and CVs come from outside and can contain "ignore previous instructions"). Very long texts are cut to a fixed size. Prompts are plain English and live in code, so they are reviewed like code.
- DI is registered in `Program.cs` (an extension in Application would need a package).

## Infrastructure (`Jobly.Infrastructure/`)

`Integrations/Llm/` (everything about the LLM in one folder, per `integrations.md`):
- `LlmOptions`: `BaseUrl`, `Model`, `TimeoutSeconds` (default 120, local models are slow), `ApiKey` (optional, empty for Ollama). Bound from `Integrations:Llm`, validated at startup. The key, if ever needed, comes from user secrets / env vars, never `appsettings.json`.
- `OpenAiCompatibleLlmClient`: typed `HttpClient` from `IHttpClientFactory`, POST `{BaseUrl}/v1/chat/completions` with JSON mode when `ExpectJson`. Returns `Result`: timeout, connection refused, 5xx become `Llm.Unavailable` (to 503), an unreadable body is `Llm.BadResponse`. Honors the `CancellationToken`.
- Logging, every call, with a comment on each statement: integration name, model, duration, input/output tokens, success, error code. **Never the prompt, the CV, or the answer.**
- `LlmErrors`, `LlmServiceCollectionExtensions.AddLlmIntegration(configuration)`, called from `Program.cs`.

`Documents/`:
- `CvTextExtractor : ICvTextExtractor`: PDF text with **PdfPig**, DOCX text with the **Open XML SDK**. Checks the extension and the first bytes (`%PDF`, `PK`), maximum 5 MB, and returns `Cv.NoTextFound` for scanned/image-only PDFs.

`Persistence/`: `Cvs` and `JobMatches` sets, configurations (match skills and gaps stored as JSON text), `SqlCvRepository`, `SqlJobMatchRepository`, migration **`AddCvAndJobMatch`** (a new migration, nothing existing is edited). `JobMatch` has a unique index on `JobId` and cascade delete with its job.

**Project reference note:** `AddHttpClient` and options validation live in the ASP.NET Core shared framework. Instead of NuGet packages, `Jobly.Infrastructure.csproj` gets `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (no download, nothing new to trust). Domain and Application stay free of ASP.NET.

## Api (`Jobly.Api/Endpoints/`)

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/cv` | Current CV (with text) | 200, 404 |
| POST | `/api/cv/file` | Upload PDF or DOCX (multipart) | 200, 400, 413 |
| PUT | `/api/cv/text` | Save pasted text | 200, 400 |
| DELETE | `/api/cv` | Remove the CV | 204, 404 |
| POST | `/api/jobs/{id}/match` | Run the Match agent | 200, 404, 409 (no CV), 503 (LLM down), 502/500 (bad answer) |
| GET | `/api/jobs/{id}/match` | Last result | 200, 404 |

- **Access (temporary, listed per security.md):** `/api/cv` and the match routes are `.AllowAnonymous()` with the same "TEMPORARY until the auth plan" comment as `/api/jobs`.
- The upload route calls `.DisableAntiforgery()` with a comment: antiforgery protects cookie sign-in, and there is no cookie sign-in yet. The auth plan revisits it.
- Request size limit on the upload (5 MB). Logs: CV saved/deleted and match run (job id, score, duration), never content.
- A new exception-free path only: all LLM and parsing failures are `Result` errors mapped by `ToProblem()`.

## React client (`client/`)

- **CV dialog** (new button "CV" in the header): shows the current CV (file name, source, characters, updated), a file picker / drop area for PDF or DOCX, a paste box, a preview of the extracted text that can be edited and saved, and a delete button.
- **Job detail panel:** new "Match with my CV" section: a button, a progress message ("Analyzing, a local model can take up to a minute"), then the score (bar and number), summary, matching skills as chips, gaps as a list, with the model name and date. With no CV it points to the CV dialog. If the LLM is down it shows the API's message and a retry button.
- `api.ts`: `uploadCv` sends `FormData` (the shared `request()` must not force a JSON `Content-Type` for it), plus `getCv`, `saveCvText`, `deleteCv`, `analyzeJob`, `getMatch`.
- Same Claude-inspired theme tokens; no new client packages.

## Docker (`docker/`)

- `docker-compose.yml`, `api` service: `Integrations__Llm__BaseUrl` from `JOBLY_LLM_BASE_URL` (default `http://host.docker.internal:11434`), `Integrations__Llm__Model` from `JOBLY_LLM_MODEL`, and `extra_hosts: host.docker.internal:host-gateway` so it also works on Linux.
- `docker/.env.example`: documents both, with a short "install Ollama and run `ollama pull <model>`" note. The default model is a small instruction-tuned model that fits in 8 GB; you choose the exact one based on your machine.

## Tests (TDD, written first)

- **Domain:** `Cv.Create` / `ReplaceText` (valid, empty, too long, trimming); `JobMatch.Create` (score 0 and 100 ok, -1 and 101 refused, summary required).
- **Application:** `CvService` for every method (upload ok, unsupported type, extractor failure, empty text, too long, paste, replace, delete, not found) using `FakeCvRepository` and `FakeCvTextExtractor`. `MatchAgent` with a hand-written `FakeLlmClient`: success, job not found, no CV, LLM failure passed through, invalid JSON, missing fields, score out of range, `GetMatch` not found. `MatchPrompt`: CV and job text appear between delimiters, the data-not-instructions sentence is present, long texts are cut.
- **Infrastructure (new project `tests/Jobly.Infrastructure.Tests`, same approved test packages):** `OpenAiCompatibleLlmClient` against a hand-written fake `HttpMessageHandler` (request shape, parsing, timeout, 5xx, connection error, bad body, cancellation). `CvTextExtractor` with a DOCX built in the test and a tiny hand-written PDF, plus wrong type, wrong magic bytes, too big, and no-text cases. No real network, no real model.
- **Api:** integration tests for every new endpoint through `JoblyApiFactory` (extended with fake CV / match repositories, `FakeLlmClient`, `FakeCvTextExtractor`): success, 400, 404, 409, 503, multipart upload. `ResultExtensionsTests`: `Unavailable` maps to 503.

## Packages and approvals (need you)

- **NuGet** (`Directory.Packages.props`, used by `Jobly.Infrastructure` only): `UglyToad.PdfPig` (PDF text) and `DocumentFormat.OpenXml` (DOCX text), latest stable versions at install time. Both are open source libraries that read untrusted files, so the 5 MB limit, the character limit and the magic-byte check matter.
- **New test project** `Jobly.Infrastructure.Tests` added to `Jobly.slnx` (no new packages).
- **`dotnet ef migrations add AddCvAndJobMatch`** and later **`dotnet ef database update`** (your approval each).
- **You do:** install Ollama on your machine and `ollama pull <model>` (I can't install programs). No user secret is needed for Ollama.
- No npm packages.

## Build order (each step: failing test, then code, `dotnet build` + `dotnet test`, then a short report)

1. Domain: `Cv`, `JobMatch`, errors, `ErrorType.Unavailable` + the 503 mapping and its test
2. Application: CV use cases + fakes
3. Application: `ILlmClient` contract, `MatchPrompt`, `MatchAgent` + `FakeLlmClient`
4. Infrastructure: new test project, `OpenAiCompatibleLlmClient`, options, DI
5. Infrastructure: `CvTextExtractor` (packages), persistence, migration (approvals)
6. Api: endpoints and integration tests
7. Docker settings, apply the migration, smoke test with real Ollama
8. React: CV dialog and match section
9. `/review-changes`, then commit in small commits on `feature/cv-and-llm`

## Verification

1. `dotnet build` and `dotnet test` pass; `dotnet format` is clean; the guard self-test still passes.
2. `docker compose -f docker/docker-compose.yml up -d --build`, apply the migration.
3. With Ollama running and a model pulled: upload a real PDF and a DOCX CV, see the extracted text, and open a job and run "Match with my CV". Score, skills and gaps appear, and reloading shows the saved result.
4. Failure checks: Ollama stopped gives a 503 and a friendly message (no crash, includes `traceId`); a scanned PDF gives `Cv.NoTextFound`; an `.exe` renamed to `.pdf` is refused; no CV gives 409.
5. API log shows model, duration and token counts for each call and **no** CV or job text.

## Not in this plan

- **Cover letter agent** (next plan: reuses `ILlmClient`, the CV, and the Match gaps).
- **Authentication** (CV and matches belong to a user; antiforgery and ownership checks are revisited then).
- **Claude (Anthropic) provider**: a second `ILlmClient` class, one `AddLlmIntegration` setting to switch.
- **Match score badge on Kanban cards** and re-matching when the CV changes.
- **Arbeidsplassen import.**
