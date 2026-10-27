using System.ComponentModel.DataAnnotations;

namespace Jobly.Api.Features.Cvs;

// Every DTO of the Cvs feature (what the API receives and returns) in one file.

/// <summary>What the client sends to save pasted or edited CV text.</summary>
public sealed class SaveCvTextRequest
{
    // The real limit (30,000 characters, after trimming) is enforced by Cv. This larger cap only stops
    // absurdly big requests from being read into memory.
    [Required, MaxLength(100_000)]
    public required string Text { get; init; }
}

/// <summary>What the API returns for the CV. The text is included because the user can read and edit it.</summary>
public sealed record CvResponse(
    Guid Id,
    string Text,
    string? FileName,
    CvSource Source,
    int CharacterCount,
    DateTimeOffset UpdatedAt)
{
    public static CvResponse From(Cv cv) => new(cv.Id, cv.Text, cv.FileName, cv.Source, cv.Text.Length, cv.UpdatedAt);
}
