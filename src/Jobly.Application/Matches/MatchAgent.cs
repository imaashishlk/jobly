using System.Text.Json;
using Jobly.Application.Cvs;
using Jobly.Application.Integrations.Llm;
using Jobly.Application.Jobs;
using Jobly.Domain.Common;
using Jobly.Domain.Jobs;
using Jobly.Domain.Matches;

namespace Jobly.Application.Matches;

public sealed class MatchAgent(
    IJobRepository jobRepository,
    ICvRepository cvRepository,
    IJobMatchRepository matchRepository,
    ILlmClient llmClient,
    TimeProvider timeProvider) : IMatchAgent
{
    // The model's answer, as we ask for it. Every field is nullable so a missing field is noticed, not turned into 0 or "".
    private sealed record Answer(int? Score, string? Summary, List<string>? MatchingSkills, List<string>? Gaps);

    private static readonly JsonSerializerOptions AnswerOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<JobMatchResponse>> AnalyzeJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetById(jobId, cancellationToken);
        if (job is null) return JobErrors.NotFound;

        var cv = await cvRepository.Get(cancellationToken);
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

        await matchRepository.Save(created.Value, cancellationToken);

        return JobMatchResponse.From(created.Value);
    }

    public async Task<Result<JobMatchResponse>> GetMatch(Guid jobId, CancellationToken cancellationToken)
    {
        var match = await matchRepository.GetByJobId(jobId, cancellationToken);
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
