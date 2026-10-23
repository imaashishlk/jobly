using Jobly.Domain.Common;

namespace Jobly.Application.Cvs;

/// <summary>
/// Reads the text out of an uploaded CV file. Infrastructure implements it with a PDF reader and a DOCX reader,
/// so the Application layer never depends on those libraries.
/// </summary>
public interface ICvTextExtractor
{
    /// <summary>
    /// Returns the text of the file, or an error such as <c>Cv.NoTextFound</c> (for example a scanned PDF)
    /// or <c>Cv.UnsupportedFileType</c> (the file's bytes are not what its name says).
    /// </summary>
    Task<Result<string>> Extract(Stream content, string fileName, CancellationToken cancellationToken);
}
