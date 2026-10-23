using System.ComponentModel.DataAnnotations;

namespace Jobly.Application.Jobs;

/// <summary>What the client sends to save the user's notes on a job. Empty or missing text clears the notes.</summary>
public sealed class UpdateNotesRequest
{
    [MaxLength(5000)]
    public string? Notes { get; init; }
}
