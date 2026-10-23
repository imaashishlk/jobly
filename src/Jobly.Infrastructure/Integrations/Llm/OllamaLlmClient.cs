using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobly.Application.Integrations.Llm;
using Jobly.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobly.Infrastructure.Integrations.Llm;

/// <summary>
/// Talks to a local Ollama server. This is the only class that knows Ollama's request and reply format.
/// We use Ollama's own /api/chat (not its OpenAI-compatible endpoint) because it accepts a JSON Schema for the
/// answer and a context size per call.
/// </summary>
internal sealed class OllamaLlmClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<OllamaLlmClient> logger) : ILlmClient
{
    private const string IntegrationName = "Ollama";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<LlmResponse>> Complete(LlmRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var stopwatch = Stopwatch.StartNew();

        // Every log line inside this block carries the integration name, so a dashboard can filter on it.
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["Integration"] = IntegrationName });

        // Our own time limit on top of the caller's token: the call ends when the user leaves OR the limit passes.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ChatTimeoutSeconds));

        try
        {
            using var httpResponse = await httpClient.PostAsJsonAsync("/api/chat", BuildBody(request, settings), JsonOptions, timeout.Token);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = httpResponse.StatusCode == HttpStatusCode.NotFound
                    ? LlmErrors.ModelNotInstalled(settings.ChatModel)
                    : LlmErrors.ServerProblem;
                return Failed(error, settings.ChatModel, stopwatch);
            }

            var reply = await httpResponse.Content.ReadFromJsonAsync<ChatReply>(JsonOptions, timeout.Token);
            if (string.IsNullOrWhiteSpace(reply?.Message?.Content)) return Failed(LlmErrors.BadResponse, settings.ChatModel, stopwatch);

            var model = reply.Model ?? settings.ChatModel;

            // One line per successful call: the numbers to watch for cost, speed and prompt size. Never the prompt or the answer.
            logger.LogInformation(
                "LLM call to {IntegrationName} model {Model} finished in {ElapsedMilliseconds} ms ({InputTokens} tokens in, {OutputTokens} tokens out)",
                IntegrationName,
                model,
                stopwatch.ElapsedMilliseconds,
                reply.PromptEvalCount,
                reply.EvalCount);

            return new LlmResponse(reply.Message.Content, model, reply.PromptEvalCount, reply.EvalCount);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Not the caller: our own time limit (or the HttpClient's) ended the call.
            return Failed(LlmErrors.TimedOut, settings.ChatModel, stopwatch);
        }
        catch (HttpRequestException)
        {
            // Nothing is listening at the address: Ollama is not running, or the address is wrong.
            return Failed(LlmErrors.NotReachable, settings.ChatModel, stopwatch);
        }
        catch (JsonException)
        {
            return Failed(LlmErrors.BadResponse, settings.ChatModel, stopwatch);
        }
    }

    private Error Failed(Error error, string model, Stopwatch stopwatch)
    {
        // A failed call is a Warning, not an Error: the app handles it and shows the user a message. The code is searchable.
        logger.LogWarning(
            "LLM call to {IntegrationName} model {Model} failed after {ElapsedMilliseconds} ms with {ErrorCode}",
            IntegrationName,
            model,
            stopwatch.ElapsedMilliseconds,
            error.Code);

        return error;
    }

    private static ChatBody BuildBody(LlmRequest request, LlmOptions settings)
    {
        // Ollama takes the schema as a JSON object. We keep it as text in Application, so parse it here.
        using var schema = request.JsonSchema is null ? null : JsonDocument.Parse(request.JsonSchema);

        return new ChatBody(
            settings.ChatModel,
            [new ChatMessage("system", request.SystemPrompt), new ChatMessage("user", request.UserPrompt)],
            Stream: false,
            schema?.RootElement.Clone(),
            new ChatOptions(settings.ContextTokens, Temperature: 0));
    }

    // ---- Ollama's JSON shapes -----------------------------------------------------------------------

    // Temperature 0 asks for the most predictable answer, which is what a scoring task wants.
    private sealed record ChatBody(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Format,
        ChatOptions Options);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatOptions([property: JsonPropertyName("num_ctx")] int NumCtx, double Temperature);

    private sealed record ChatReply(
        string? Model,
        ReplyMessage? Message,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);

    private sealed record ReplyMessage(string? Content);
}
