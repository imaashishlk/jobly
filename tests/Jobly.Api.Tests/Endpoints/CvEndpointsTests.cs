using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobly.Api.Common;
using Jobly.Api.Features.Cvs;

namespace Jobly.Api.Tests.Endpoints;

// A new in-memory API per test (xUnit creates this class once per test), so tests can't see each other's CV.
public sealed class CvEndpointsTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly JoblyApiFactory factory = new();
    private readonly HttpClient client;

    public CvEndpointsTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    [Fact]
    public async Task GetCv_NoCvYet_ReturnsNotFoundProblemWithErrorCode()
    {
        // Act
        var response = await client.GetAsync("/api/cv", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Cv.NotFound", await ReadErrorCode(response));
    }

    [Fact]
    public async Task GetCv_CvExists_ReturnsOkWithTheText()
    {
        // Arrange
        await factory.Seed(Cv.Create("Ola Nordmann, C# developer", CvSource.Uploaded, "cv.pdf", Now).Value);

        // Act
        var response = await client.GetAsync("/api/cv", TestContext.Current.CancellationToken);

        // Assert: the enum travels as text ("Uploaded"), like every other enum in the API.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CvResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Ola Nordmann, C# developer", body!.Text);
        Assert.Equal(CvSource.Uploaded, body.Source);
        Assert.Equal("cv.pdf", body.FileName);
    }

    [Fact]
    public async Task PutCvText_NewText_ReturnsOkAndStoresAPastedCv()
    {
        // Act
        var response = await client.PutAsJsonAsync("/api/cv/text", new { text = "Pasted CV text" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await factory.StoredCv();
        Assert.Equal("Pasted CV text", stored!.Text);
        Assert.Equal(CvSource.Pasted, stored.Source);
    }

    [Fact]
    public async Task PutCvText_EmptyText_ReturnsBadRequestAndStoresNothing()
    {
        // Act: rejected by the [Required] annotation before the service runs.
        var response = await client.PutAsJsonAsync("/api/cv/text", new { text = "" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.StoredCv());
    }

    [Fact]
    public async Task PutCvText_TextLongerThanACvMayBe_ReturnsBadRequestWithErrorCode()
    {
        // Act: within the request size cap, so it is the Domain rule that answers.
        var response = await client.PutAsJsonAsync("/api/cv/text", new { text = new string('a', Cv.MaxTextLength + 1) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cv.TooLong", await ReadErrorCode(response));
    }

    [Fact]
    public async Task PostCvFile_PdfFile_ReturnsOkAndStoresAnUploadedCv()
    {
        // Arrange: the fake extractor stands in for the PDF reader.
        factory.CvTextExtractor.Result = Result.Success("Text from the PDF");
        using var form = FormWithFile("cv.pdf", [1, 2, 3]);

        // Act
        var response = await client.PostAsync("/api/cv/file", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CvResponse>(JsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Text from the PDF", body!.Text);
        Assert.Equal("cv.pdf", body.FileName);
        Assert.Equal(CvSource.Uploaded, (await factory.StoredCv())!.Source);
    }

    [Fact]
    public async Task PostCvFile_UnsupportedFileType_ReturnsBadRequestWithErrorCode()
    {
        // Arrange
        using var form = FormWithFile("cv.exe", [1, 2, 3]);

        // Act
        var response = await client.PostAsync("/api/cv/file", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cv.UnsupportedFileType", await ReadErrorCode(response));
        Assert.Null(await factory.StoredCv());
    }

    [Fact]
    public async Task PostCvFile_ReaderFindsNoText_ReturnsBadRequestWithErrorCode()
    {
        // Arrange: like a scanned PDF.
        factory.CvTextExtractor.Result = Result.Failure<string>(CvErrors.NoTextFound);
        using var form = FormWithFile("scan.pdf", [1, 2, 3]);

        // Act
        var response = await client.PostAsync("/api/cv/file", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cv.NoTextFound", await ReadErrorCode(response));
    }

    [Fact]
    public async Task PostCvFile_FileLargerThanFiveMegabytes_ReturnsBadRequestWithErrorCode()
    {
        // Arrange: one byte over the limit.
        using var form = FormWithFile("big.pdf", new byte[(int)CvService.MaxFileBytes + 1]);

        // Act
        var response = await client.PostAsync("/api/cv/file", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cv.FileTooLarge", await ReadErrorCode(response));
    }

    [Fact]
    public async Task PostCvFile_NoFileInTheRequest_ReturnsBadRequest()
    {
        // Arrange: a form with no file in it.
        using var form = new MultipartFormDataContent { { new StringContent("hello"), "note" } };

        // Act
        var response = await client.PostAsync("/api/cv/file", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCv_CvExists_ReturnsNoContentAndRemovesIt()
    {
        // Arrange
        await factory.Seed(Cv.Create("My CV", CvSource.Pasted, null, Now).Value);

        // Act
        var response = await client.DeleteAsync("/api/cv", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await factory.StoredCv());
    }

    [Fact]
    public async Task DeleteCv_NoCv_ReturnsNotFound()
    {
        // Act
        var response = await client.DeleteAsync("/api/cv", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static MultipartFormDataContent FormWithFile(string fileName, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<string?> ReadErrorCode(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return problem.RootElement.GetProperty("errorCode").GetString();
    }
}
