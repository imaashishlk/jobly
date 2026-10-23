using Jobly.Api.Common;
using Jobly.Application.Jobs;
using Jobly.Domain.Jobs;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jobly.Api.Endpoints.Jobs;

public static class JobEndpoints
{
    // Called once from Program.cs: app.MapJobEndpoints();
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs")
            .WithTags("Jobs")
            // TEMPORARY: there is no sign-in yet, so the whole group is public. The auth plan replaces this with
            // .RequireAuthorization() and adds ownership checks and 401/403 tests (see .claude/rules/security.md).
            .AllowAnonymous();

        jobs.MapGet("/", GetJobs);
        jobs.MapGet("/{id:guid}", GetJob);
        jobs.MapPost("/", CreateJob);
        jobs.MapPut("/{id:guid}", UpdateJob);
        jobs.MapPut("/{id:guid}/status", ChangeStatus);
        jobs.MapPut("/{id:guid}/notes", UpdateNotes);
        jobs.MapDelete("/{id:guid}", DeleteJob);
    }

    // The CancellationToken is filled in by ASP.NET Core and fires when the browser closes the request.
    // Passing it down stops the database query too.
    private static async Task<Ok<IReadOnlyList<JobResponse>>> GetJobs(
        [FromQuery] ApplicationStatus? status,
        IJobService jobService,
        CancellationToken cancellationToken)
    {
        var jobs = await jobService.ListJobs(status, cancellationToken);
        return TypedResults.Ok(jobs);
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> GetJob(
        Guid id,
        IJobService jobService,
        CancellationToken cancellationToken)
    {
        var result = await jobService.GetJob(id, cancellationToken);

        // Failure: the shared helper turns the error into 404, 400, 409, ... (see Common/ResultExtensions.cs).
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Created<JobResponse>, ProblemHttpResult>> CreateJob(
        CreateJobRequest request,
        IJobService jobService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await jobService.CreateJob(request, cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Shows in the dashboard when a job is saved, and which source it came from.
        logger.LogInformation("Job {JobId} created from source {Source}", result.Value.Id, result.Value.Source);

        // 201 Created, with the address of the new job in the Location header.
        return TypedResults.Created($"/api/jobs/{result.Value.Id}", result.Value);
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> UpdateJob(
        Guid id,
        UpdateJobRequest request,
        IJobService jobService,
        CancellationToken cancellationToken)
    {
        var result = await jobService.UpdateJob(id, request, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> ChangeStatus(
        Guid id,
        ChangeStatusRequest request,
        IJobService jobService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await jobService.ChangeStatus(id, request, cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Shows how jobs move across the board. Handy for spotting a card that was moved by mistake.
        logger.LogInformation("Job {JobId} moved to status {Status}", result.Value.Id, result.Value.Status);

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<Ok<JobResponse>, ProblemHttpResult>> UpdateNotes(
        Guid id,
        UpdateNotesRequest request,
        IJobService jobService,
        CancellationToken cancellationToken)
    {
        var result = await jobService.UpdateNotes(id, request, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteJob(
        Guid id,
        IJobService jobService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await jobService.DeleteJob(id, cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Deleting can't be undone, so it is always logged.
        logger.LogInformation("Job {JobId} deleted", id);

        return TypedResults.NoContent();
    }
}
