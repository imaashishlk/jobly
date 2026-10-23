using Jobly.Domain.Common;

namespace Jobly.Domain.Cvs;

/// <summary>
/// The user's CV as plain text. There is one CV for the whole app until sign-in exists.
/// </summary>
public sealed class Cv
{
    /// <summary>The longest CV text we keep. It keeps the database small and the LLM prompt in a sensible size.</summary>
    public const int MaxTextLength = 30_000;

    // Private: the only way to get a Cv is Create, which validates first.
    private Cv(string text, CvSource source, string? fileName, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Text = text;
        Source = source;
        FileName = fileName;
        UpdatedAt = now;
    }

    // Used by EF Core when it loads a row from the database.
    private Cv()
    {
        Text = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Text { get; private set; }

    public CvSource Source { get; private set; }

    public string? FileName { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<Cv> Create(string text, CvSource source, string? fileName, DateTimeOffset now)
    {
        var error = ValidateText(text);
        if (error is not null) return error;

        var cleanFileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        return new Cv(text.Trim(), source, cleanFileName, now);
    }

    /// <summary>
    /// Replaces the text, for example when the user fixes what was read from a file.
    /// The file name and source stay as they were. Nothing changes if the new text is not valid.
    /// </summary>
    public Result ReplaceText(string text, DateTimeOffset now)
    {
        var error = ValidateText(text);
        if (error is not null) return error;

        Text = text.Trim();
        UpdatedAt = now;

        return Result.Success();
    }

    private static Error? ValidateText(string text) => text switch
    {
        _ when string.IsNullOrWhiteSpace(text) => CvErrors.TextRequired,
        _ when text.Trim().Length > MaxTextLength => CvErrors.TooLong,
        _ => null,
    };
}
