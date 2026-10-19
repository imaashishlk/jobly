# Security: endpoint access

The API is built with **Minimal APIs**, so access is declared with method calls instead of attributes:

| Meaning | Minimal API call | (Controller equivalent, for reference) |
|---|---|---|
| Signed-in users only | `.RequireAuthorization()` | `[Authorize]` |
| Anyone, no sign-in | `.AllowAnonymous()` | `[AllowAnonymous]` |
| Must pass a named rule | `.RequireAuthorization(Policies.Employer)` | `[Authorize(Policy = ...)]` |

## Rules

### Every route group declares its access once. An endpoint has its own call only when it differs from its group.
```csharp
public static class JobEndpoints
{
    // Called once from Program.cs: app.MapJobEndpoints();
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs")
            .WithTags("Jobs")
            .RequireAuthorization();                         // Whole group: signed-in users only.

        jobs.MapGet("/", GetJobs)
            .AllowAnonymous();                               // Public: visitors can browse listings before signing up.

        jobs.MapGet("/{id:guid}", GetJob);                   // Inherits the group rule.

        jobs.MapPost("/", CreateJob)
            .RequireAuthorization(Policies.Employer);        // Stricter: only employers can post jobs.
    }
}
```
**Why:** Opening an endpoints file tells you its access immediately, at the top of the group. The exceptions stand out because they're the only endpoints with their own call.

### Protected by default: a fallback policy requires an authenticated user.
`Program.cs` sets `FallbackPolicy` to "authenticated user required", so an endpoint someone forgot to mark is **closed** (`401`), not open. It's implemented and tested as part of the solution-setup plan.
```csharp
builder.Services.AddAuthorization(options =>
{
    // Safety net: any endpoint without RequireAuthorization() or AllowAnonymous() requires a signed-in user.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
```
**Why:** Mistakes fail safe. Forgetting to declare access gives a `401`, never an accidental data leak. The group declarations are for readability, and the fallback is the safety net.

### Every `.AllowAnonymous()` gets a comment saying why the endpoint is public, and the feature plan must list it.
**Why:** Public endpoints are the attack surface. Each one should be a deliberate, reviewed decision, not a convenience.

### Use named policies (`.RequireAuthorization(Policies.Employer)`), with the names kept as constants in one `Policies` class. No raw role strings.
**Why:** A typo in a string like `"Emplyer"` compiles fine and silently locks everyone out, or lets everyone in. Constants are checked by the compiler, and policies keep all access rules in one place.

### Every protected endpoint has an integration test proving it returns `401` without a user, and `403` for the wrong user where a policy applies.
**Why:** Access rules are behavior. They need tests just like business logic, or a refactor can quietly open an endpoint.

### Ownership is checked in the service, after loading the record.
Access declarations decide which *kind* of user may call an endpoint. They can't decide whether this user may touch *this specific* record. For example, an employer may edit only their own job postings:
```csharp
public async Task<Result<JobResponse>> UpdateJob(Guid jobId, UpdateJobRequest request, CancellationToken cancellationToken)
{
    var job = await jobRepository.GetById(jobId, cancellationToken);
    if (job is null) return JobErrors.NotFound;                       // Guard clause: no such job.

    // Ownership check: the signed-in employer must be the one who posted this job.
    if (job.EmployerId != currentUser.Id) return JobErrors.NotOwner;   // The API maps this error to 403 Forbidden.
    ...
}
```
- The current user comes from an `ICurrentUser` interface declared in Application and implemented in Api from `HttpContext.User`. Services never touch `HttpContext` directly.
- A failed check returns a `Result` error (for example `Job.NotOwner`), which the API maps to `403 Forbidden` in its shared Result-to-HTTP `switch`.
- Every ownership check gets unit tests for both cases: the owner is allowed, and a non-owner is refused.

**Why:** Without this check, any signed-in employer could edit another employer's jobs just by changing the ID in the URL. That's the most common access bug in web APIs (IDOR, "insecure direct object reference"). Keeping the check in the service means it applies however the service is called, and `ICurrentUser` keeps it testable without a real web request.

## Still to decide (in its own plan)

How users sign in, which decides the authentication scheme: for example JWT bearer tokens issued by our API, or Microsoft Entra ID. The same plan defines the roles and named policies (`Policies.Employer` and so on). It needs a NuGet package (such as `Microsoft.AspNetCore.Authentication.JwtBearer`) and gets its own plan in `docs/plans/` for your approval. Until then, the rules above apply to every endpoint we write.
