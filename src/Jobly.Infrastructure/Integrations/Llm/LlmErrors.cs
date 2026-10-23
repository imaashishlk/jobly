using Jobly.Domain.Common;

namespace Jobly.Infrastructure.Integrations.Llm;

/// <summary>
/// The errors the LLM integration can return. All "model is down or too slow" cases share the code
/// <c>Llm.Unavailable</c> (HTTP 503) and differ only in the message, which tells the user what to do.
/// </summary>
internal static class LlmErrors
{
    public static readonly Error NotReachable = Error.Unavailable(
        "Llm.Unavailable",
        "The language model cannot be reached. Is Ollama running?");

    public static readonly Error TimedOut = Error.Unavailable(
        "Llm.Unavailable",
        "The language model took too long to answer. Try again, or choose a smaller model.");

    public static readonly Error ServerProblem = Error.Unavailable(
        "Llm.Unavailable",
        "The language model had a problem. Please try again.");

    // Ollama answers 404 when the model was never downloaded. Naming the exact command saves the user a search.
    public static Error ModelNotInstalled(string model) => Error.Unavailable(
        "Llm.Unavailable",
        $"The model '{model}' is not installed. Run: ollama pull {model}");

    // The model answered, but the reply is not in the shape Ollama promises. That is a bug or a version mismatch.
    public static readonly Error BadResponse = Error.Failure(
        "Llm.BadResponse",
        "The language model sent an answer that could not be read.");
}
