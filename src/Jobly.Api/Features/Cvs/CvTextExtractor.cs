using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Jobly.Api.Common;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Jobly.Api.Features.Cvs;

/// <summary>
/// Reads the text out of an uploaded CV file. <see cref="CvTextExtractor"/> implements it with a PDF reader and a DOCX reader,
/// so <see cref="CvService"/> never depends on those libraries, and tests can replace it with a fake.
/// </summary>
public interface ICvTextExtractor
{
    /// <summary>
    /// Returns the text of the file, or an error such as <c>Cv.NoTextFound</c> (for example a scanned PDF)
    /// or <c>Cv.UnsupportedFileType</c> (the file's bytes are not what its name says).
    /// </summary>
    Task<Result<string>> Extract(Stream content, string fileName, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the text out of an uploaded CV: PDF with PdfPig, DOCX with Microsoft's Open XML SDK.
/// The file comes from outside, so it is treated as untrusted: the first bytes must match the file name,
/// only the first pages are read, and reading stops once there is more text than a CV may hold.
/// </summary>
internal sealed class CvTextExtractor(ILogger<CvTextExtractor> logger) : ICvTextExtractor
{
    /// <summary>A CV is a few pages. A huge PDF must not keep the server busy, so only this many pages are read.</summary>
    public const int MaxPdfPages = 20;

    // A PDF file starts with "%PDF-". A DOCX file is a zip file, which starts with "PK" and two more bytes.
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public async Task<Result<string>> Extract(Stream content, string fileName, CancellationToken cancellationToken)
    {
        // Both readers need a seekable stream, and an upload stream is not always one. The file is at most 5 MB (checked before this point).
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        // The name says what the sender claims, the first bytes say what the file is. Both must agree.
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" when StartsWith(bytes, PdfSignature) => ReadPdf(bytes, cancellationToken),
            ".docx" when StartsWith(bytes, ZipSignature) => ReadDocx(bytes, cancellationToken),
            _ => CvErrors.UnsupportedFileType,
        };
    }

    private Result<string> ReadPdf(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(bytes);
            var text = new StringBuilder();

            foreach (var page in document.GetPages().Take(MaxPdfPages))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Reads the page in reading order (columns and lines), not in the order the file happens to store the words.
                text.Append(ContentOrderTextExtractor.GetText(page).Replace("\r\n", "\n")).Append('\n');
                if (text.Length > Cv.MaxTextLength) break;
            }

            return ToResult(text);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreadable(exception);
        }
    }

    private Result<string> ReadDocx(byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var document = WordprocessingDocument.Open(stream, isEditable: false);
            var text = new StringBuilder();

            // Descendants also finds the paragraphs inside tables, which many CVs use for layout.
            var paragraphs = document.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [];
            foreach (var paragraph in paragraphs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = paragraph.InnerText;
                if (string.IsNullOrWhiteSpace(line)) continue;

                text.Append(line).Append('\n');
                if (text.Length > Cv.MaxTextLength) break;
            }

            return ToResult(text);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unreadable(exception);
        }
    }

    private static Result<string> ToResult(StringBuilder text)
    {
        var result = text.ToString().Trim();
        return result.Length == 0 ? CvErrors.NoTextFound : result;
    }

    private Result<string> Unreadable(Exception exception)
    {
        // Damaged, cut-off and password-protected files make these libraries throw many kinds of exceptions.
        // For a file from a user that is a normal outcome, so it becomes a Result error. Only the exception type
        // is logged: nothing from inside the file, which can hold personal data.
        logger.LogWarning("A CV file could not be read ({ExceptionType})", exception.GetType().Name);

        return CvErrors.Unreadable;
    }

    private static bool StartsWith(byte[] bytes, byte[] signature) => bytes.AsSpan().StartsWith(signature);
}
