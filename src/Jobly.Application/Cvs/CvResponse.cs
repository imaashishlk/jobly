using Jobly.Domain.Cvs;

namespace Jobly.Application.Cvs;

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
