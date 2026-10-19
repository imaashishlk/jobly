# Testing

### Write tests with xUnit, test first (TDD).
**Why:** Tests written first describe what the code should do before it exists. That keeps the design focused, and a failing test proves the test works.

### Name test projects `<ProjectName>.Tests`, for example `Jobly.Application.Tests`.
**Why:** You can tell at a glance which project a test project covers.

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
Put them in a `Fakes/` folder in the test project (for example `FakeJobRepository`, `FixedTimeProvider`). Each fake is a small `sealed` class that implements the interface.
**Why:** There's no extra package, and a hand-written fake is plain C# you can read and step through in the debugger. Mocking libraries hide behavior behind setup syntax that's harder to learn.

### Endpoint integration tests use `WebApplicationFactory` from `Microsoft.AspNetCore.Mvc.Testing`.
This is the only approved test helper package. It's added when the API test project is created, with your approval at that moment.
**Why:** It starts the real API in memory and sends real HTTP requests, so routing, validation, DI, and JSON are all tested together. Integrations and the database are swapped for fakes in its DI setup.

### Test both the success and failure paths of a `Result`.
**Why:** With the Result pattern, failures are normal return values. They're behavior and need tests just like success does.

### Never skip, delete, or weaken a failing test to make the build pass. Fix the code, or ask.
**Why:** A test that's silenced can't catch the bug it was written for. "All tests pass" has to stay meaningful.

## Run a single test

```bash
dotnet test --filter "FullyQualifiedName~Jobly.Application.Tests.JobServiceTests.CreateJob_TitleIsEmpty_ReturnsValidationError"
```
