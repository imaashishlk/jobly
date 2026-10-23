using Jobly.Domain.Jobs;

namespace Jobly.Application.Jobs;

/// <summary>What the API returns for a job: the posting plus the user's tracking of it. Never the entity itself.</summary>
public sealed record JobResponse(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    string Description,
    string? Url,
    JobSource Source,
    string? ExternalId,
    ApplicationStatus Status,
    string? Notes,
    DateTimeOffset? AppliedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static JobResponse From(Job job) => new(
        job.Id,
        job.Title,
        job.Company,
        job.Location,
        job.Description,
        job.Url,
        job.Source,
        job.ExternalId,
        job.Application.Status,
        job.Application.Notes,
        job.Application.AppliedAt,
        job.CreatedAt,
        job.Application.UpdatedAt);
}
