using System.ComponentModel.DataAnnotations;

namespace Jobly.Application.Jobs;

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
