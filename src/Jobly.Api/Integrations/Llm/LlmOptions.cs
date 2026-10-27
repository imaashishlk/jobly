using System.ComponentModel.DataAnnotations;

namespace Jobly.Api.Integrations.Llm;

/// <summary>
/// Settings for the language model, bound from the <c>Integrations:Llm</c> section of the configuration.
/// They are checked when the app starts, so a missing or wrong setting fails with a clear message right away.
/// No API key is needed for a local Ollama. A provider that needs one would get it from user secrets or an
/// environment variable, never from appsettings.json.
/// </summary>
internal sealed class LlmOptions
{
    public const string SectionName = "Integrations:Llm";

    /// <summary>Where Ollama listens, for example http://localhost:11434. Inside Docker this is http://host.docker.internal:11434.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The model that answers questions, for example qwen2.5:7b.</summary>
    [Required]
    public string ChatModel { get; set; } = string.Empty;

    /// <summary>The model that turns text into vectors, for example bge-m3.</summary>
    [Required]
    public string EmbeddingModel { get; set; } = string.Empty;

    /// <summary>How long one question may take before we give up. Generous, because a model on a CPU is slow.</summary>
    [Range(1, 3600)]
    public int ChatTimeoutSeconds { get; set; } = 300;

    /// <summary>How long one embedding call may take. Short, because embedding is quick and should never block saving.</summary>
    [Range(1, 600)]
    public int EmbeddingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// How many tokens of text the model may read at once (its context window). We set it on every call:
    /// if a prompt is longer than the window, the model silently drops the beginning, which holds our instructions.
    /// </summary>
    [Range(1024, 131_072)]
    public int ContextTokens { get; set; } = 8192;
}
