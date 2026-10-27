using System.Net;
using System.Text.Json;
using Jobly.Api.Common;
using Jobly.Api.Integrations.Llm;
using Jobly.Api.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jobly.Api.UnitTests.Integrations.Llm;

public sealed class OllamaLlmClientTests
{
    private const string GoodReply = """
        {"model":"test-chat","message":{"role":"assistant","content":"{\"score\":72}"},"done":true,"prompt_eval_count":120,"eval_count":30}
        """;

    private static readonly LlmRequest Request = new("You are a recruiter.", "Compare these.", """{"type":"object"}""");

    private static OllamaLlmClient CreateClient(FakeHttpMessageHandler handler, int chatTimeoutSeconds = 30)
    {
        var options = new LlmOptions
        {
            BaseUrl = "http://ollama.test:11434",
            ChatModel = "test-chat",
            EmbeddingModel = "test-embed",
            ChatTimeoutSeconds = chatTimeoutSeconds,
            EmbeddingTimeoutSeconds = 5,
            ContextTokens = 8192,
        };

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        return new OllamaLlmClient(httpClient, Options.Create(options), NullLogger<OllamaLlmClient>.Instance);
    }

    [Fact]
    public async Task Complete_ValidReply_ReturnsTextModelAndTokenCounts()
    {
        // Arrange
        var client = CreateClient(FakeHttpMessageHandler.Replying(HttpStatusCode.OK, GoodReply));

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("""{"score":72}""", result.Value.Text);
        Assert.Equal("test-chat", result.Value.Model);
        Assert.Equal(120, result.Value.InputTokens);
        Assert.Equal(30, result.Value.OutputTokens);
    }

    [Fact]
    public async Task Complete_AnyRequest_SendsModelMessagesSchemaAndContextSizeToTheChatEndpoint()
    {
        // Arrange
        var handler = FakeHttpMessageHandler.Replying(HttpStatusCode.OK, GoodReply);
        var client = CreateClient(handler);

        // Act
        await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.Equal("/api/chat", handler.LastPath);
        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal("test-chat", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("You are a recruiter.", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal("object", root.GetProperty("format").GetProperty("type").GetString());
        Assert.Equal(8192, root.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(0, root.GetProperty("options").GetProperty("temperature").GetInt32());
    }

    [Fact]
    public async Task Complete_NoSchema_OmitsTheFormatField()
    {
        // Arrange
        var handler = FakeHttpMessageHandler.Replying(HttpStatusCode.OK, GoodReply);
        var client = CreateClient(handler);

        // Act
        await client.Complete(new LlmRequest("System", "User", JsonSchema: null), CancellationToken.None);

        // Assert
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.TryGetProperty("format", out _));
    }

    [Fact]
    public async Task Complete_ServerCannotBeReached_ReturnsUnavailable()
    {
        // Arrange: nothing is listening, as when Ollama is not running.
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Llm.Unavailable", result.Error.Code);
        Assert.Equal(ErrorType.Unavailable, result.Error.Type);
    }

    [Fact]
    public async Task Complete_ServerAnswers500_ReturnsUnavailable()
    {
        // Arrange
        var client = CreateClient(FakeHttpMessageHandler.Replying(HttpStatusCode.InternalServerError, """{"error":"boom"}"""));

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Llm.Unavailable", result.Error.Code);
    }

    [Fact]
    public async Task Complete_ModelIsNotInstalled_TellsTheUserWhichCommandToRun()
    {
        // Arrange: Ollama answers 404 when the model was never pulled.
        var client = CreateClient(FakeHttpMessageHandler.Replying(HttpStatusCode.NotFound, """{"error":"model 'test-chat' not found"}"""));

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Llm.Unavailable", result.Error.Code);
        Assert.Contains("ollama pull test-chat", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_ModelAnswersTooSlowly_ReturnsUnavailable()
    {
        // Arrange: the fake never answers, so only the one-second timeout can end the call.
        var handler = new FakeHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler, chatTimeoutSeconds: 1);

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Llm.Unavailable", result.Error.Code);
        Assert.Contains("too long", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Complete_CallerCancels_ThrowsOperationCanceledInsteadOfAnError()
    {
        // Arrange: the user closed the page while the model was working.
        using var cancellation = new CancellationTokenSource();
        var handler = new FakeHttpMessageHandler(async (_, token) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler);

        // Act and Assert: cancelling is not a failure of the model, so it is not turned into a Result error.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Complete(Request, cancellation.Token));
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("""{"model":"test-chat","done":true}""")]
    [InlineData("""{"model":"test-chat","message":{"role":"assistant","content":""},"done":true}""")]
    public async Task Complete_ReplyCannotBeRead_ReturnsBadResponse(string body)
    {
        // Arrange: not JSON, no message at all, or an empty message.
        var client = CreateClient(FakeHttpMessageHandler.Replying(HttpStatusCode.OK, body));

        // Act
        var result = await client.Complete(Request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Llm.BadResponse", result.Error.Code);
    }
}
