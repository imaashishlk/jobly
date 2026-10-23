# CV + local LLM + Match agent + embeddings (PostgreSQL / pgvector)

## Context

The job tracker is done and the database is now PostgreSQL with pgvector ready (`pgvector 0.8.0`, not switched on yet). This plan adds the first AI features:

1. **CV**: upload a PDF or DOCX (or paste text), keep the text.
2. **Local LLM integration** (Ollama): the app talks to a model through `ILlmClient`.
3. **Match agent**: compares the CV with a job and returns a score, matching skills, and gaps.
4. **Embeddings + pgvector**: a quick "CV similarity" badge on every card, "best match first" sorting, and retrieval (RAG), so the Match agent reads only the CV parts that matter for that job.

Decisions already made:
- Upload **PDF or DOCX**; paste text is the fallback (scanned PDFs have no text).
- **Local LLM first**, so the CV never leaves the machine and nothing costs money. `ILlmClient` / `IEmbeddingClient` stay provider-neutral: a Claude or OpenAI client can be added later as one more class.
- **Embeddings scope: badge + RAG.**
- No sign-in yet, so there is **one CV for the whole app** (the auth plan ties it to a user).
- Ollama is already installed.

**Your machine** (checked): 15.7 GB RAM, no dedicated GPU (Intel Iris Xe only), so models run on the CPU. Installed: `qwen2.5-coder:7b` (tuned for code, not for CVs) and `llama3.2` (3B, fast but weak at following a strict JSON answer).

**Model suggestions** (all configurable in `docker/.env`, nothing hard-coded):
- **Chat: `qwen2.5:7b`** (about 4.7 GB). Good at instructions and JSON. Expect one analysis to take roughly 1 to 3 minutes on this CPU; retrieval keeps the prompt short, which helps. Faster alternative: `gemma3:4b` (also good with several languages). `llama3.2` stays usable for quick tests.
- **Embeddings: `bge-m3`** (about 1.2 GB, 1024 dimensions). It handles Norwegian and English together, which matters for Arbeidsplassen ads and a mixed-language CV.
- You pull them yourself: `ollama pull qwen2.5:7b` and `ollama pull bge-m3`.

**Why Ollama's own API, not the OpenAI-compatible one:** the native `/api/chat` lets us (a) pass a **JSON schema** so even a small model must answer in our exact shape, and (b) set the context size (`num_ctx`), so a long prompt is never silently cut at the start. `/api/embed` does batch embedding. The interfaces in Application stay neutral; only `OllamaLlmClient` knows Ollama.

Two phases, so something works end to end early and risk stays small:
- **Phase A:** CV + LLM + Match agent (the agent reads the whole CV, shortened to a size limit).
- **Phase B:** embeddings, badge, sorting, and RAG for the Match agent (with a fallback to Phase A behavior).

Branch `feature/cv-and-llm`, created from `feature/switch-to-postgres`. After approval this text replaces the draft in `docs/plans/cv-and-llm.md`.

## Domain (`Jobly.Domain/`)

Phase A:
- `Cvs/Cv`: `Id`, `Text`, `FileName?`, `Source` (`Uploaded` | `Pasted`), `UpdatedAt`. `Cv.Create` returns `Result<Cv>`: text required, at most 30,000 characters. `ReplaceText` follows the same rules. Private setters and constructor, like `Job`.
- `Cvs/CvErrors`: `Cv.NotFound`, `Cv.TextRequired`, `Cv.TooLong`, `Cv.UnsupportedFileType`, `Cv.FileTooLarge`, `Cv.NoTextFound`.
- `Matches/JobMatch`: `Id`, `JobId`, `Score` (0 to 100), `Summary`, `MatchingSkills`, `Gaps`, `Model`, `CreatedAt`. `JobMatch.Create` returns a `Result`: score outside 0 to 100 is an error (never silently clamped), summary required.
- `Matches/MatchErrors`: `Match.NotFound`, `Match.CvRequired` (Conflict), `Match.InvalidResponse`.
- `Common/ErrorType`: add **`Unavailable`** (an outside service is down or too slow). `ToProblem()` maps it to **503**. The switch has no `_` fallback, so the build fails until it is handled; test in `ResultExtensionsTests`.

Phase B:
- `Embeddings/EmbeddingVector` rules: exactly **1024** numbers (a Domain constant, because the database column has a fixed size). `CvChunk` (`Id`, `CvId`, `Index`, `Text`, `Embedding`, `Model`) and `JobEmbedding` (`JobId`, `Embedding`, `Model`, `CreatedAt`). Both use plain `float[]`, so Domain still references nothing (the database type is mapped in Infrastructure).

## Application (`Jobly.Application/`)

Phase A:
- `Cvs/`: `ICvRepository` (`Get`, `Save` replaces the single CV, `Delete`), `ICvTextExtractor` (`Extract(Stream, fileName, ct)` returns `Result<string>`), `ICvService` / `CvService` (`GetCv`, `UploadCv`, `SaveCvText`, `DeleteCv`), DTOs `SaveCvTextRequest`, `CvResponse`.
- `Integrations/Llm/`: `ILlmClient.Complete(LlmRequest, ct)` returns `Result<LlmResponse>`. `LlmRequest` (system prompt, user prompt, optional **JSON schema**), `LlmResponse` (text, model, input tokens, output tokens).
- `Matches/`: `IJobMatchRepository` (`GetByJobId`, `Save` keeps the latest match per job), `IMatchAgent` / `MatchAgent` (`AnalyzeJob`, `GetMatch`), `MatchPrompt`, `JobMatchResponse`.
- `AnalyzeJob` steps: load job (404), load CV (`Match.CvRequired`), build prompt, call `ILlmClient`, parse `{score, summary, matchingSkills[], gaps[]}`, create `JobMatch` through the Domain, save. LLM failures come back unchanged as `Result` errors.
- **Prompt rules** (in `MatchPrompt`, tested): the CV and job text sit between clear delimiters and the system prompt says they are **data, never instructions** (ads and CVs come from outside and can say "ignore previous instructions"). A fixed size budget (about 9,000 characters in total) cuts long texts. Prompts are plain English and live in code.

Phase B:
- `Integrations/Llm/IEmbeddingClient.Embed(IReadOnlyList<string>, ct)` returns `Result<IReadOnlyList<float[]>>`.
- `Cvs/CvChunker`: splits the CV into about 800-character chunks with a small overlap, on paragraph or sentence boundaries. Deterministic and unit-tested.
- `CvService`: after a CV is saved it chunks and embeds it. If embedding fails the **CV is still saved** (the text is the source of truth); the response says `embeddingsReady: false`.
- `JobService`: after create or update it embeds the job text, **best effort**: a failure never blocks saving the job; it logs a warning.
- `Embeddings/IEmbeddingRepository` (`ReplaceCvChunks`, `SaveJobEmbedding`, `GetNearestCvChunks(jobId, count)`, `GetSimilarityScores()`, `GetMissing...`) and `EmbeddingService` with `Rebuild()` (re-embeds the CV and every job whose embedding is missing or from another model).
- `JobResponse` gets `similarityScore` (nullable int). **Meaning:** `round(100 x average cosine similarity of the 3 CV chunks closest to the job)`. It is a quick relative hint ("this looks closer to your CV"), not a verdict; the Match agent is the real analysis. The UI says so.
- `MatchAgent` in Phase B: take the **5 nearest CV chunks** plus the job text. If embeddings are not ready it falls back to the Phase A behavior (whole CV, cut to the size budget), so it always works.

DI registrations go in `Program.cs` (an extension in Application would need a package).

## Infrastructure (`Jobly.Infrastructure/`)

`Integrations/Llm/` (everything about the LLM in one folder, per `integrations.md`):
- `LlmOptions` (section `Integrations:Llm`): `BaseUrl`, `ChatModel`, `EmbeddingModel`, `ChatTimeoutSeconds` (default 300, the CPU is slow), `EmbeddingTimeoutSeconds` (default 30), `ContextTokens` (default 8192). Validated at startup, so a missing setting fails with a clear message. No secrets are needed for local Ollama.
- `OllamaLlmClient : ILlmClient, IEmbeddingClient`: typed `HttpClient` from `IHttpClientFactory`. Chat uses `/api/chat` (`stream: false`, `format` = our JSON schema, `options.num_ctx`); embeddings use `/api/embed`. Timeout, connection refused or 5xx become `Llm.Unavailable` (503); an unreadable or wrong-size answer is `Llm.BadResponse`. Honors `CancellationToken`.
- Logging, one statement per call, each with a comment: integration name, model, duration, input/output token counts (Ollama returns them), success, error code. **Never the prompt, the CV, or the answer.**
- `LlmErrors`, `LlmServiceCollectionExtensions.AddLlmIntegration(configuration)`.

`Documents/`: `CvTextExtractor : ICvTextExtractor`: PDF text with **PdfPig**, DOCX text with the **Open XML SDK**. Checks extension and first bytes (`%PDF`, `PK`), 5 MB maximum, `Cv.NoTextFound` for image-only PDFs.

`Persistence/`:
- Phase A: `Cvs` and `JobMatches` (skills and gaps stored as JSON text), repositories, migration **`AddCvAndJobMatch`**. `JobMatch` has a unique index on `JobId` and is deleted with its job.
- Phase B: NuGet **`Pgvector.EntityFrameworkCore`**; `UseNpgsql(..., o => o.UseVector())`; migration **`AddEmbeddings`** that runs `CREATE EXTENSION vector`, creates `CvChunks` and `JobEmbeddings` with `vector(1024)` columns and an **HNSW index** (cosine) on `CvChunks`. A value converter maps `float[]` to the database vector type.
- The two vector queries (nearest chunks, and one similarity score per job in a single statement) use `ORDER BY embedding <=> ...`. Where plain LINQ cannot express it, the SQL is written once, in the repository, with a comment.
- Changing the embedding model to one with a different size needs a new migration and a rebuild; this is written in the code comments and in `.env.example`.

`Jobly.Infrastructure.csproj` gets `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for `AddHttpClient` and options validation (no extra package; Domain and Application stay free of ASP.NET).

## Api (`Jobly.Api/Endpoints/`)

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/cv` | Current CV with text | 200, 404 |
| POST | `/api/cv/file` | Upload PDF or DOCX (multipart) | 200, 400, 413 |
| PUT | `/api/cv/text` | Save pasted or edited text | 200, 400 |
| DELETE | `/api/cv` | Remove the CV | 204, 404 |
| POST | `/api/jobs/{id}/match` | Run the Match agent | 200, 404, 409 (no CV), 503 (LLM down), 502 (bad answer) |
| GET | `/api/jobs/{id}/match` | Last result | 200, 404 |
| POST | `/api/embeddings/rebuild` | Phase B: re-embed CV and jobs | 200, 409 (no CV), 503 |

- `GET /api/jobs` now includes `similarityScore` (Phase B).
- **Access (temporary, listed per security.md):** the new groups are `.AllowAnonymous()` with the same "TEMPORARY until the auth plan" comment. The upload route calls `.DisableAntiforgery()` with a comment (antiforgery protects cookie sign-in, and there is none yet; the auth plan revisits it). Upload size limit 5 MB.
- Logs: CV saved/deleted, match run (job id, score, duration), rebuild counts. Never content.

## React client (`client/`)

- **CV dialog** (button "CV" in the header): file picker or drop area for PDF/DOCX, paste box, the extracted text shown and editable, delete, and (Phase B) "Search index: ready / not ready" with a Rebuild button.
- **Job detail panel:** section "Match with my CV": a button, a progress message ("This machine runs the model on the CPU, so it can take a few minutes"), then score bar, summary, matching skills as chips, gaps as a list, model name and date. Without a CV it points to the CV dialog; if the model is down it shows the message and a retry button.
- **Cards (Phase B):** badge "CV 72%" with a tooltip explaining it is a quick similarity hint; a sort switch "Newest / Best match" inside each column.
- `api.ts`: `uploadCv` sends `FormData` (the shared `request()` must not force a JSON `Content-Type` for it), plus the new calls. Same theme tokens; no new client packages.

## Docker (`docker/`)

- `api` service: `Integrations__Llm__BaseUrl` from `JOBLY_LLM_BASE_URL` (default `http://host.docker.internal:11434`), `...__ChatModel` from `JOBLY_LLM_CHAT_MODEL` (default `qwen2.5:7b`), `...__EmbeddingModel` from `JOBLY_LLM_EMBEDDING_MODEL` (default `bge-m3`), and `extra_hosts: host.docker.internal:host-gateway` (so it also works on Linux).
- `docker/.env.example` documents these and the two `ollama pull` commands.
- **Known risk:** Ollama listens on `127.0.0.1` by default. Docker Desktop on Windows normally lets a container reach it through `host.docker.internal`, but this is checked in the smoke test. If it does not work, the fallback is running the API on the host with `dotnet run` (it reaches `127.0.0.1` directly). I will **not** switch Ollama to listen on all network interfaces, because that would expose the model to your network.

## Tests (TDD, written first)

- **Domain:** `Cv.Create` / `ReplaceText`; `JobMatch.Create` (0 and 100 ok, -1 and 101 refused, summary required); embedding vector size rules.
- **Application:** `CvService` (upload ok, unsupported type, extractor failure, empty, too long, paste, replace, delete, not found; Phase B: embedding failure still saves the CV); `MatchAgent` with a hand-written `FakeLlmClient` (success, job not found, no CV, LLM failure passed through, invalid JSON, missing fields, score out of range, `GetMatch` not found; Phase B: uses nearest chunks, falls back to the whole CV); `MatchPrompt` (delimiters, the data-not-instructions sentence, size budget); `CvChunker`; `JobService` embedding best effort; `EmbeddingService.Rebuild`.
- **Infrastructure (project `Jobly.Infrastructure.Tests` already exists):** `OllamaLlmClient` against a hand-written fake `HttpMessageHandler` (request shape including the schema and `num_ctx`, parsing, token counts, timeout, 5xx, refused connection, bad body, wrong vector size, cancellation); `CvTextExtractor` with a DOCX built in the test and a tiny hand-written PDF (plus wrong type, wrong magic bytes, too big, no text); model tests for the new tables, the `vector(1024)` columns, the HNSW index, and the extension line in the create script. No network, no model.
- **Api:** integration tests for every new endpoint through `JoblyApiFactory` (extended with fake repositories, `FakeLlmClient`, `FakeCvTextExtractor`): success, 400, 404, 409, 503, multipart upload; `similarityScore` in the list.
- **Not unit-testable without a database:** the two vector SQL queries. They are proven in the Docker smoke test with real pgvector (below).

## Packages and approvals (need you)

- **NuGet** (Infrastructure only): `UglyToad.PdfPig`, `DocumentFormat.OpenXml` (Phase A), `Pgvector.EntityFrameworkCore` (Phase B), latest stable versions at install time. The first two read untrusted files, so the size limits and byte checks matter.
- **`dotnet ef migrations add`** twice (`AddCvAndJobMatch`, `AddEmbeddings`) and **`dotnet ef database update`** after each.
- **You do:** `ollama pull qwen2.5:7b` and `ollama pull bge-m3` (about 6 GB in total), and keep Ollama running. No user secret is needed.
- No npm packages. Guardrail files are not touched.

## Build order (each step: failing test, then code, build and test, short report)

**Phase A**
1. Domain: `Cv`, `JobMatch`, errors, `ErrorType.Unavailable` + 503 mapping and its test
2. Application: CV use cases + fakes
3. Application: `ILlmClient` contract, `MatchPrompt`, `MatchAgent` + `FakeLlmClient`
4. Infrastructure: `OllamaLlmClient` (chat), options, DI, tests with a fake handler
5. Infrastructure: `CvTextExtractor` (packages), persistence, migration (approvals)
6. Api: endpoints and integration tests
7. Docker settings, apply the migration, check the container reaches Ollama, real match run
8. React: CV dialog and match section; **checkpoint**: `/review-changes`, commit, push

**Phase B**
9. Domain + Application: embedding types, `CvChunker`, `IEmbeddingClient`, embedding repository contract, `EmbeddingService`, changes to `CvService` / `JobService` / `MatchAgent`
10. Infrastructure: embeddings in `OllamaLlmClient`, pgvector package, migration `AddEmbeddings`, vector queries (approvals)
11. Api: `similarityScore`, rebuild endpoint, tests
12. React: badge, sorting, search-index status
13. Docker smoke test with real pgvector and Ollama; `/review-changes`, commit, push

## Verification

1. `dotnet build` and `dotnet test` pass; `dotnet format` clean; the guard self-test passes.
2. `docker compose -f docker/docker-compose.yml up -d --build`; migrations applied.
3. Upload a real PDF CV and a DOCX CV: the extracted text shows and can be edited.
4. Add or open a job and run "Match with my CV": score, skills and gaps appear (allow a few minutes), and a reload shows the saved result.
5. Phase B: the search index becomes ready after saving the CV; each card shows a similarity badge; "Best match" sorting reorders cards; the Match prompt uses the nearest chunks (checked from the token counts in the log, which drop compared with the whole-CV run).
6. Failure checks: Ollama stopped gives a 503 and a friendly message with `traceId`, and saving a job still works; a scanned PDF gives `Cv.NoTextFound`; an `.exe` renamed to `.pdf` is refused; no CV gives 409; embedding failure never blocks saving a CV or a job.
7. The API log shows model, duration and token counts for every call and **no** CV or job text.

## Not in this plan

- Cover letter agent (next plan: reuses `ILlmClient`, the CV chunks and the Match gaps).
- Authentication (the CV, matches and embeddings belong to a user; antiforgery and ownership are revisited).
- A Claude or OpenAI provider, re-matching automatically when the CV changes, and the Arbeidsplassen import.
