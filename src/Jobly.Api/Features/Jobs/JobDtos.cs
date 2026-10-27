using System.ComponentModel.DataAnnotations;

namespace Jobly.Api.Features.Jobs;

// Every DTO of the Jobs feature (what the API receives and returns) in one file.
// The API never returns the Job entity itself, only JobResponse.

/// <summary>What the client sends to save a new job.</summary>
public sealed class CreateJobRequest
{
    // [Required] and [MaxLength] are checked by the API before the service runs and answered with 400.
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(200)]
    public required string Company { get; init; }

    [MaxLength(200)]
    public string? Location { get; init; }

    // Job ads can be long, so the limit is generous.
    [Required, MaxLength(20_000)]
    public required string Description { get; init; }

    [Url, MaxLength(2000)]
    public string? Url { get; init; }
}

/// <summary>What the client sends to edit a job's fields. Source and status are not editable here.</summary>
public sealed class UpdateJobRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(200)]
    public required string Company { get; init; }

    [MaxLength(200)]
    public string? Location { get; init; }

    [Required, MaxLength(20_000)]
    public required string Description { get; init; }

    [Url, MaxLength(2000)]
    public string? Url { get; init; }
}

/// <summary>What the client sends to move a job to another Kanban column.</summary>
public sealed class ChangeStatusRequest
{
    public required ApplicationStatus Status { get; init; }
}

/// <summary>What the client sends to save the user's notes on a job. Empty or missing text clears the notes.</summary>
public sealed class UpdateNotesRequest
{
    [MaxLength(5000)]
    public string? Notes { get; init; }
}

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
