# Testing

### Write tests with xUnit, test first (TDD).
**Why:** Tests written first describe what the code should do before it exists. That keeps the design focused, and a failing test proves the test works.

### There are two test projects for `Jobly.Api`: `Jobly.Api.UnitTests` and `Jobly.Api.Tests`.
- `Jobly.Api.UnitTests`: entities, services, the CV reader, the LLM client, the EF model, and the slice rules. Folders mirror `src/Jobly.Api` (`Features/Jobs/`, `Common/`, ...).
- `Jobly.Api.Tests`: endpoints over real HTTP (`WebApplicationFactory`).

**Why:** The fast tests are kept apart from the ones that start the whole API, and the names say which project they cover.

### Name test methods `MethodName_Scenario_ExpectedResult`.
For example: `CreateJob_TitleIsEmpty_ReturnsValidationError`.
**Why:** When a test fails, its name alone tells you what broke, in which situation, and what was expected, before you open the file.

### Structure each test as Arrange / Act / Assert, with a comment marking each section.
**Why:** Every test reads the same way: set up, do the one thing, check the result. That makes tests quick to understand.

### Every service method gets unit tests. Every endpoint gets at least one integration test.
**Why:**
- Unit tests check business logic in isolation, and they're fast.
- Integration tests check that routing, validation, DI, and serialization work together through real HTTP.

### Use fakes for time and integrations: a fixed `TimeProvider`, a fake `ILlmClient`.
**Why:** Tests must give the same result every run, offline, without API keys or cost. See [code-style.md](code-style.md) and [integrations.md](integrations.md).

### Write fakes by hand. No mocking library.
Put them in a `Fakes/` folder in the test project (for example `FakeLlmClient`, `FakeCvTextExtractor`, `FixedTimeProvider`). Each fake is a small `sealed` class that implements the interface.
**Why:** There's no extra package, and a hand-written fake is plain C# you can read and step through in the debugger. Mocking libraries hide behavior behind setup syntax that's harder to learn.

### The database is never faked. Services and endpoints run against a real in-memory SQLite database.
Services take `JoblyDbContext` directly, so tests use `TestDatabase` (unit tests) or `JoblyApiFactory.Seed(...)` (endpoint tests) to add starting data. Check what was really saved through a **fresh** context (`database.NewContext()`), not the one the service used. After adding starting data, clear the change tracker, so the service starts like a real request.
**Why:** A hand-written fake list can't enforce unique indexes, foreign keys, or cascades, so it hides database bugs. SQLite does, and it's fast and needs no Docker. It is not PostgreSQL, so Npgsql-specific details (column types, the migration snapshot) are checked in `JoblyDbContextModelTests`. The extra package, `Microsoft.EntityFrameworkCore.Sqlite`, is for tests only. Test code stores `DateTimeOffset` as a number, because SQLite can't sort by it.

### Endpoint integration tests use `WebApplicationFactory` from `Microsoft.AspNetCore.Mvc.Testing`.
**Why:** It starts the real API in memory and sends real HTTP requests, so routing, validation, DI, and JSON are all tested together. The database is swapped for in-memory SQLite, and the LLM and file reader for fakes, in its DI setup.

### Test both the success and failure paths of a `Result`.
**Why:** With the Result pattern, failures are normal return values. They're behavior and need tests just like success does.

### Never skip, delete, or weaken a failing test to make the build pass. Fix the code, or ask.
**Why:** A test that's silenced can't catch the bug it was written for. "All tests pass" has to stay meaningful.

## Run a single test

```bash
dotnet test --project tests/Jobly.Api.UnitTests --filter-method "Jobly.Api.UnitTests.Features.Jobs.JobServiceTests.CreateJob_TitleIsEmpty_ReturnsValidationErrorAndSavesNothing"
dotnet test --project tests/Jobly.Api.UnitTests --filter-class "Jobly.Api.UnitTests.Features.Jobs.JobServiceTests"   # all tests in one class
```

Tests run on the Microsoft Testing Platform (switched on in `global.json`), the runner xUnit v3 uses on .NET 10. That's why the filter options are `--filter-method` and `--filter-class` instead of the older `--filter "FullyQualifiedName~..."`.
