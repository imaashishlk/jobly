using System.Text.Json;
using Jobly.Api.Common;
using Jobly.Api.Features.Jobs;
using Jobly.Api.Integrations.Llm;
using Jobly.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.Features.Matches;

// Matches is the one feature allowed to read the Jobs and Cvs tables, because a match compares a job with the CV.
public sealed class MatchAgent(JoblyDbContext dbContext, ILlmClient llmClient, TimeProvider timeProvider) : IMatchAgent
{
    // The model's answer, as we ask for it. Every field is nullable so a missing field is noticed, not turned into 0 or "".
    private sealed record Answer(int? Score, string? Summary, List<string>? MatchingSkills, List<string>? Gaps);

    private static readonly JsonSerializerOptions AnswerOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<JobMatchResponse>> AnalyzeJob(Guid jobId, CancellationToken cancellationToken)
    {
        // AsNoTracking on both: the job and the CV are only read to build the prompt, never changed here.
        var job = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(found => found.Id == jobId, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        var cv = await dbContext.Cvs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (cv is null) return MatchErrors.CvRequired;

        // The slow part (seconds to minutes on a local model). The token lets the user cancel by closing the page.
        var response = await llmClient.Complete(MatchPrompt.Build(job, cv.Text), cancellationToken);
        if (response.IsFailure) return response.Error;

        var answer = ParseAnswer(response.Value.Text);
        if (answer is null) return MatchErrors.InvalidResponse;

        // The Domain checks the values (score 0 to 100, summary present). If the model broke a rule, that is an
        // unusable answer, not a mistake of the caller, so it is reported as InvalidResponse.
        var created = JobMatch.Create(
            jobId,
            answer.Score!.Value,
            answer.Summary!,
            answer.MatchingSkills!,
            answer.Gaps!,
            response.Value.Model,
            timeProvider.GetUtcNow());
        if (created.IsFailure) return MatchErrors.InvalidResponse;

        // Replace the earlier result for this job (the unique index allows only one) in one transaction,
        // so a failed save cannot leave the job without its old result either.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.JobMatches.Where(existing => existing.JobId == jobId).ExecuteDeleteAsync(cancellationToken);
        dbContext.JobMatches.Add(created.Value);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return JobMatchResponse.From(created.Value);
    }

    public async Task<Result<JobMatchResponse>> GetMatch(Guid jobId, CancellationToken cancellationToken)
    {
        // AsNoTracking: a result is only read and shown, never changed. A new run creates a new result instead.
        var match = await dbContext.JobMatches.AsNoTracking().FirstOrDefaultAsync(found => found.JobId == jobId, cancellationToken);
        if (match is null) return MatchErrors.NotFound;

        return JobMatchResponse.From(match);
    }

    // Returns null when the text is not JSON, has a wrong type (a decimal or text score), or lacks a field.
    private static Answer? ParseAnswer(string text)
    {
        try
        {
            var answer = JsonSerializer.Deserialize<Answer>(text, AnswerOptions);
            var complete = answer is { Score: not null, Summary: not null, MatchingSkills: not null, Gaps: not null };
            return complete ? answer : null;
        }
        catch (JsonException)
        {
            // A model that answers in prose is a normal outcome, not a crash, so it becomes a Result error above.
            return null;
        }
    }
}
