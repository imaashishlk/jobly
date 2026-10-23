using System.ComponentModel.DataAnnotations;

namespace Jobly.Application.Cvs;

/// <summary>What the client sends to save pasted or edited CV text.</summary>
public sealed class SaveCvTextRequest
{
    // The real limit (30,000 characters, after trimming) is enforced by the Domain. This larger cap only stops
    // absurdly big requests from being read into memory.
    [Required, MaxLength(100_000)]
    public required string Text { get; init; }
}
