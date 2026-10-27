using Jobly.Api.Common;

namespace Jobly.Api.Features.Cvs;

/// <summary>All the expected failures for the CV, in one place so codes and messages stay consistent.</summary>
public static class CvErrors
{
    public static readonly Error NotFound = Error.NotFound("Cv.NotFound", "No CV has been added yet.");

    public static readonly Error TextRequired = Error.Validation("Cv.TextRequired", "The CV text is required.");

    public static readonly Error TooLong = Error.Validation("Cv.TooLong", $"The CV text can be at most {Cv.MaxTextLength} characters.");

    public static readonly Error UnsupportedFileType = Error.Validation("Cv.UnsupportedFileType", "Only PDF and DOCX files are supported.");

    public static readonly Error FileTooLarge = Error.Validation("Cv.FileTooLarge", "The file is too large. The limit is 5 MB.");

    // A file with the right header that the reader still cannot open: damaged, cut off, or protected with a password.
    public static readonly Error Unreadable = Error.Validation(
        "Cv.Unreadable",
        "The file could not be read. It may be damaged or protected with a password.");

    public static readonly Error NoTextFound = Error.Validation(
        "Cv.NoTextFound",
        "No text could be read from the file. It may be a scanned image. Paste the text instead.");
}
