using System.ComponentModel.DataAnnotations;

namespace Jobly.Application.Jobs;

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
