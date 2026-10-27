using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Jobly.Api.Features.Cvs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobly.Api.UnitTests.Features.Cvs;

public sealed class CvTextExtractorTests
{
    private readonly CvTextExtractor extractor = new(NullLogger<CvTextExtractor>.Instance);

    [Fact]
    public async Task Extract_PdfWithText_ReturnsTheText()
    {
        // Arrange
        using var file = new MemoryStream(CreatePdf("Hello CV"));

        // Act
        var result = await extractor.Extract(file, "cv.pdf", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Hello", result.Value, StringComparison.Ordinal);
        Assert.Contains("CV", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Extract_PdfWithoutAnyText_ReturnsNoTextFound()
    {
        // Arrange: like a scanned CV, the page holds no text a program can read.
        using var file = new MemoryStream(CreatePdf(""));

        // Act
        var result = await extractor.Extract(file, "scan.pdf", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NoTextFound", result.Error.Code);
    }

    [Fact]
    public async Task Extract_PdfWithManyPages_ReadsOnlyTheFirstPages()
    {
        // Arrange: a CV is a few pages. A huge PDF must not keep the server busy.
        var pages = Enumerable.Range(1, CvTextExtractor.MaxPdfPages + 10).Select(number => $"Page{number}end").ToArray();
        using var file = new MemoryStream(CreatePdf(pages));

        // Act
        var result = await extractor.Extract(file, "long.pdf", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Page1end", result.Value, StringComparison.Ordinal);
        Assert.Contains($"Page{CvTextExtractor.MaxPdfPages}end", result.Value, StringComparison.Ordinal);
        Assert.DoesNotContain($"Page{CvTextExtractor.MaxPdfPages + 1}end", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Extract_Docx_ReturnsEachParagraphOnItsOwnLine()
    {
        // Arrange
        using var file = CreateDocx("Ola Nordmann", "C# developer");

        // Act
        var result = await extractor.Extract(file, "My CV.docx", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Ola Nordmann\nC# developer", result.Value);
    }

    [Fact]
    public async Task Extract_DocxWithNoText_ReturnsNoTextFound()
    {
        // Arrange
        using var file = CreateDocx();

        // Act
        var result = await extractor.Extract(file, "empty.docx", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NoTextFound", result.Error.Code);
    }

    [Fact]
    public async Task Extract_DocxWithHugeText_StopsReadingOnceTheLimitIsPassed()
    {
        // Arrange: 200 paragraphs of 1,000 characters is far more than any CV.
        using var file = CreateDocx(Enumerable.Range(0, 200).Select(_ => new string('a', 1_000)).ToArray());

        // Act
        var result = await extractor.Extract(file, "huge.docx", CancellationToken.None);

        // Assert: still more than the CV limit, so the CV rules answer "too long", but memory is not filled with 200,000 characters.
        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.Length, Cv.MaxTextLength + 1, Cv.MaxTextLength * 2);
    }

    [Theory]
    [InlineData("cv.pdf", "MZ this is really a program, not a PDF")]
    [InlineData("cv.docx", "%PDF-1.4 this is a PDF, not a DOCX")]
    public async Task Extract_BytesDoNotMatchTheFileName_ReturnsUnsupportedFileType(string fileName, string content)
    {
        // Arrange: renaming a file does not change what it is, so the first bytes are checked.
        using var file = new MemoryStream(Encoding.ASCII.GetBytes(content));

        // Act
        var result = await extractor.Extract(file, fileName, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.UnsupportedFileType", result.Error.Code);
    }

    [Fact]
    public async Task Extract_UnknownExtension_ReturnsUnsupportedFileType()
    {
        // Arrange
        using var file = new MemoryStream(Encoding.ASCII.GetBytes("plain text"));

        // Act
        var result = await extractor.Extract(file, "cv.txt", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.UnsupportedFileType", result.Error.Code);
    }

    [Theory]
    [InlineData("cv.pdf", "%PDF-1.4 but then only garbage follows")]
    [InlineData("cv.docx", "PK\u0003\u0004 but then only garbage follows")]
    public async Task Extract_CorruptFileWithTheRightHeader_ReturnsUnreadableInsteadOfCrashing(string fileName, string content)
    {
        // Arrange: damaged, truncated or password-protected files make the readers throw. That is a normal outcome here.
        using var file = new MemoryStream(Encoding.Latin1.GetBytes(content));

        // Act
        var result = await extractor.Extract(file, fileName, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.Unreadable", result.Error.Code);
    }

    [Fact]
    public async Task Extract_CallerCancels_ThrowsOperationCanceled()
    {
        // Arrange
        using var file = new MemoryStream(CreatePdf("Hello"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act and Assert: cancelling is not a broken file, so it is not turned into an error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.Extract(file, "cv.pdf", cancellation.Token));
    }

    // ---- Test files, built by hand so no binary fixtures are needed -----------------------------------

    private static MemoryStream CreateDocx(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(paragraphs.Select(text => new Paragraph(new Run(new Text(text))))));
        }

        stream.Position = 0;
        return stream;
    }

    // A minimal valid PDF with one line of text per page. The cross-reference table needs the byte offset of every
    // object, so they are measured while the file is written.
    private static byte[] CreatePdf(params string[] pageTexts)
    {
        var pageCount = pageTexts.Length;
        var fontObjectNumber = 3 + (pageCount * 2);

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pageCount} >>",
        };

        for (var index = 0; index < pageCount; index++)
        {
            var contentObjectNumber = 4 + (index * 2);
            var content = $"BT /F1 18 Tf 20 100 Td ({pageTexts[index]}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Contents {contentObjectNumber} 0 R /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> >>");
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var crossReferenceStart = builder.Length;
        builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) builder.Append($"{offset:D10} 00000 n \n");
        builder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{crossReferenceStart}\n%%EOF");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
