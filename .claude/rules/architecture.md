# Architecture: vertical slices

Jobly is one ASP.NET Core project, `src/Jobly.Api`, organised by **feature**, not by technical layer.

```
src/Jobly.Api/
  Program.cs                    Composition root: DI registrations and the request pipeline
  Features/
    Jobs/                       Everything about jobs, in one folder
      Job.cs, JobApplication.cs         Entities (plain C#), each file also holds the enum it owns (JobSource, ApplicationStatus)
      JobErrors.cs                      Error codes this feature returns
      JobDtos.cs                        All DTOs (API input and output): the four requests and JobResponse
      JobService.cs                     IJobService and JobService: the use cases, using JoblyDbContext directly
      JobEndpoints.cs                   Minimal API routes and their access rules
      JobConfiguration.cs               EF Core table mapping for Job and JobApplication
    Cvs/  Matches/              Same shape
  Common/                       Result, Error, ErrorType, ResultExtensions (ToProblem), GlobalExceptionHandler
  Persistence/                  JoblyDbContext, AddPersistence(), Migrations/
  Integrations/Llm/             ILlmClient, OllamaLlmClient, LlmOptions, LlmErrors, AddLlmIntegration()
```

## Rules

### A feature lives in `Features/<Name>/`, with everything about it in that folder.
**Why:** To understand or change "jobs" you open one folder. Before, the same feature was spread over four projects.

### Small types that belong together share one file: an enum with its entity, an interface with its service, all DTOs in `<Feature>Dtos.cs`, the EF mappings of a feature in one file.
Big classes (an entity with real logic, a service, the endpoints) keep their own file.
**Why:** A feature folder of 15 files with 8-line files is harder to read than 7 files. Opening `JobService.cs` shows the contract and the code that fulfils it together, and `JobDtos.cs` shows everything the API accepts and returns on one screen.

### Entities are plain C#: no EF Core and no ASP.NET Core types.
EF Core mapping goes in a separate `IEntityTypeConfiguration<T>` class in the same feature folder.
**Why:** The business rules (`Job.Update`, `Cv.Create`) stay easy to unit test and easy to read, and the database details stay in one file next to them.

### Services use `JoblyDbContext` directly. There is no repository layer.
**Why:** `DbContext` already is a unit of work and a repository. A wrapper around it adds files and indirection without adding anything a junior developer needs. Services are tested against a real in-memory database (see [testing.md](testing.md)).

### Services never touch `HttpContext`. Only `*Endpoints.cs` files use ASP.NET Core types.
**Why:** A service can then be called from a test, a background job, or another endpoint without a web request. Users reach services through `ICurrentUser` (see [security.md](security.md)).

### Interfaces exist at real boundaries: `ILlmClient`, `ICvTextExtractor`, and the service interfaces the endpoints use.
**Why:** The LLM and the file reader are outside the app, slow, and cost money or CPU, so tests replace them with fakes. Putting an interface in front of every class is noise.

### `Matches` may use `Jobs` and `Cvs`. `Jobs` and `Cvs` must not use each other or `Matches`.
**Why:** A match compares a job with the CV, so it has to read both. Everywhere else, a feature that needs another feature's code is a sign the code belongs in `Common/`. The test `SliceRulesTests` reads the source files and fails when someone breaks this, because one project no longer lets the compiler do it.

### Shared code goes in `Common/`, `Persistence/` or `Integrations/<Name>/`. Never copy it between features.
**Why:** Two copies drift apart. A shared home has one place to fix a bug.

### A new feature is a new folder, plus one registration line in `Program.cs` and one `Map<Feature>Endpoints()` call.
**Why:** Adding a feature does not touch another feature, so features can be reviewed, finished, or dropped on their own.

### Migrations stay in `Persistence/Migrations/`, generated with `--project src/Jobly.Api`.
**Why:** There is one `DbContext` and one model snapshot for the whole database. Features share the schema, even though their code is separate.
