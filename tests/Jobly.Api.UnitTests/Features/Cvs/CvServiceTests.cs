using Jobly.Api.Common;
using Jobly.Api.Features.Cvs;
using Jobly.Api.UnitTests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.UnitTests.Features.Cvs;

public sealed class CvServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database = new();
    private readonly FakeCvTextExtractor extractor = new();
    private readonly CvService service;

    public CvServiceTests()
    {
        service = new CvService(database.Context, extractor, new FixedTimeProvider(Now));
    }

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task GetCv_NoCvYet_ReturnsNotFound()
    {
        // Act
        var result = await service.GetCv(CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetCv_CvExists_ReturnsTextAndDetails()
    {
        // Arrange
        await AddCv(Cv.Create("My CV", CvSource.Uploaded, "cv.pdf", Now).Value);

        // Act
        var result = await service.GetCv(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("My CV", result.Value.Text);
        Assert.Equal("cv.pdf", result.Value.FileName);
        Assert.Equal(CvSource.Uploaded, result.Value.Source);
        Assert.Equal(5, result.Value.CharacterCount);
    }

    [Fact]
    public async Task SaveCvText_NoCvYet_CreatesPastedCv()
    {
        // Act
        var result = await service.SaveCvText(new SaveCvTextRequest { Text = "  Pasted CV  " }, CancellationToken.None);

        // Assert
        var stored = await StoredCv();
        Assert.True(result.IsSuccess);
        Assert.Equal("Pasted CV", stored!.Text);
        Assert.Equal(CvSource.Pasted, stored.Source);
        Assert.Null(stored.FileName);
    }

    [Fact]
    public async Task SaveCvText_CvExists_ReplacesTextAndKeepsFileNameAndSource()
    {
        // Arrange: the user uploaded a file and now fixes the extracted text by hand.
        await AddCv(Cv.Create("Old text", CvSource.Uploaded, "cv.docx", Now.AddDays(-1)).Value);

        // Act
        var result = await service.SaveCvText(new SaveCvTextRequest { Text = "Fixed text" }, CancellationToken.None);

        // Assert
        var stored = await StoredCv();
        Assert.True(result.IsSuccess);
        Assert.Equal("Fixed text", stored!.Text);
        Assert.Equal("cv.docx", stored.FileName);
        Assert.Equal(CvSource.Uploaded, stored.Source);
        Assert.Equal(Now, stored.UpdatedAt);
    }

    [Fact]
    public async Task SaveCvText_TextIsEmpty_ReturnsTextRequiredAndSavesNothing()
    {
        // Act
        var result = await service.SaveCvText(new SaveCvTextRequest { Text = "   " }, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TextRequired", result.Error.Code);
        Assert.Null(await StoredCv());
    }

    [Fact]
    public async Task SaveCvText_TextIsTooLong_ReturnsTooLong()
    {
        // Act
        var result = await service.SaveCvText(new SaveCvTextRequest { Text = new string('a', Cv.MaxTextLength + 1) }, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TooLong", result.Error.Code);
    }

    [Theory]
    [InlineData("cv.pdf")]
    [InlineData("My CV.DOCX")]
    public async Task UploadCv_SupportedFile_SavesUploadedCvWithExtractedText(string fileName)
    {
        // Arrange
        extractor.Result = Result.Success("Text from the file");

        // Act
        var result = await service.UploadCv(new MemoryStream([1, 2, 3]), fileName, 3, CancellationToken.None);

        // Assert
        var stored = await StoredCv();
        Assert.True(result.IsSuccess);
        Assert.Equal("Text from the file", stored!.Text);
        Assert.Equal(CvSource.Uploaded, stored.Source);
        Assert.Equal(fileName, stored.FileName);
    }

    [Theory]
    [InlineData("cv.exe")]
    [InlineData("cv.txt")]
    [InlineData("cv")]
    public async Task UploadCv_UnsupportedExtension_ReturnsUnsupportedFileTypeWithoutReadingIt(string fileName)
    {
        // Act
        var result = await service.UploadCv(new MemoryStream([1, 2, 3]), fileName, 3, CancellationToken.None);

        // Assert: refused before the extractor is asked to read untrusted bytes.
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.UnsupportedFileType", result.Error.Code);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public async Task UploadCv_FileIsTooLarge_ReturnsFileTooLargeWithoutReadingIt()
    {
        // Act
        var result = await service.UploadCv(new MemoryStream([1]), "cv.pdf", CvService.MaxFileBytes + 1, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.FileTooLarge", result.Error.Code);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public async Task UploadCv_ExtractorFails_ReturnsThatErrorAndKeepsTheOldCv()
    {
        // Arrange
        var oldCv = Cv.Create("Old CV", CvSource.Pasted, null, Now.AddDays(-1)).Value;
        await AddCv(oldCv);
        extractor.Result = Result.Failure<string>(CvErrors.NoTextFound);

        // Act
        var result = await service.UploadCv(new MemoryStream([1]), "scan.pdf", 1, CancellationToken.None);

        // Assert
        var stored = await StoredCv();
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NoTextFound", result.Error.Code);
        Assert.Equal(oldCv.Id, stored!.Id);
        Assert.Equal("Old CV", stored.Text);
    }

    [Fact]
    public async Task UploadCv_ExtractedTextIsBlank_ReturnsNoTextFound()
    {
        // Arrange: some files read fine but contain no words.
        extractor.Result = Result.Success("   \n ");

        // Act
        var result = await service.UploadCv(new MemoryStream([1]), "empty.docx", 1, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NoTextFound", result.Error.Code);
    }

    [Fact]
    public async Task UploadCv_CvAlreadyExists_ReplacesIt()
    {
        // Arrange
        await AddCv(Cv.Create("Old CV", CvSource.Pasted, null, Now.AddDays(-1)).Value);
        extractor.Result = Result.Success("New CV");

        // Act
        await service.UploadCv(new MemoryStream([1]), "cv.pdf", 1, CancellationToken.None);

        // Assert: there is still only one CV, and it is the new one.
        await using var check = database.NewContext();
        var all = await check.Cvs.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("New CV", Assert.Single(all).Text);
    }

    [Fact]
    public async Task DeleteCv_CvExists_RemovesIt()
    {
        // Arrange
        await AddCv(Cv.Create("My CV", CvSource.Pasted, null, Now).Value);

        // Act
        var result = await service.DeleteCv(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(await StoredCv());
    }

    [Fact]
    public async Task DeleteCv_NoCv_ReturnsNotFound()
    {
        // Act
        var result = await service.DeleteCv(CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.NotFound", result.Error.Code);
    }

    // Puts a CV straight into the database, bypassing the service, so each test controls its own starting data.
    private async Task AddCv(Cv cv)
    {
        database.Context.Cvs.Add(cv);
        await database.Context.SaveChangesAsync();

        // In the real API every request gets a fresh DbContext that tracks nothing yet. Forgetting what the test
        // just added makes the service load the CV from the database, as it would in a request.
        database.Context.ChangeTracker.Clear();
    }

    // Reads the CV through a fresh context, so the test sees what is really stored and not what is only in memory.
    private async Task<Cv?> StoredCv()
    {
        await using var check = database.NewContext();
        return await check.Cvs.SingleOrDefaultAsync();
    }
}
