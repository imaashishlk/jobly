using Jobly.Api.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobly.Api.Features.Matches;

public static class MatchEndpoints
{
    // Called once from Program.cs: app.MapMatchEndpoints();
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        // The route group carries the job id, so both handlers receive it as a plain parameter.
        var match = app.MapGroup("/api/jobs/{jobId:guid}/match")
            .WithTags("Match")
            // TEMPORARY: no sign-in yet, so the group is public. The auth plan replaces this with .RequireAuthorization()
            // and checks that the job belongs to the signed-in user (see .claude/rules/security.md).
            .AllowAnonymous();

        match.MapPost("/", AnalyzeJob);
        match.MapGet("/", GetMatch);
    }

    // Asks the language model, so it can take minutes on a CPU. The token stops the work if the user closes the page.
    private static async Task<Results<Ok<JobMatchResponse>, ProblemHttpResult>> AnalyzeJob(
        Guid jobId,
        IMatchAgent matchAgent,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await matchAgent.AnalyzeJob(jobId, cancellationToken);

        if (result.IsFailure)
        {
            // Warning: handled, and the user sees a message. The code tells "model is down" apart from "no CV yet".
            logger.LogWarning("Analyzing job {JobId} failed with {ErrorCode}", jobId, result.Error.Code);
            return result.Error.ToProblem();
        }

        // The score and model are what you look at when a result seems strange. The summary is not logged.
        logger.LogInformation("Job {JobId} analyzed: score {Score} by model {Model}", jobId, result.Value.Score, result.Value.Model);

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<Ok<JobMatchResponse>, ProblemHttpResult>> GetMatch(
        Guid jobId,
        IMatchAgent matchAgent,
        CancellationToken cancellationToken)
    {
        var result = await matchAgent.GetMatch(jobId, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }
}
