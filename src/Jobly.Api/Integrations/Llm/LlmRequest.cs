namespace Jobly.Api.Integrations.Llm;

/// <summary>
/// What the app asks a language model. It says nothing about any provider (Ollama, Claude, ...): the
/// provider's own class in Infrastructure turns this into that provider's request.
/// </summary>
/// <param name="SystemPrompt">The standing instructions: who the model is, the rules, the answer format.</param>
/// <param name="UserPrompt">The material for this one question (here: the job ad and the CV).</param>
/// <param name="JsonSchema">
/// When set, the model must answer with JSON that fits this JSON Schema. A schema makes even a small
/// local model answer in the exact shape the code expects. Null means a free-text answer.
/// </param>
public sealed record LlmRequest(string SystemPrompt, string UserPrompt, string? JsonSchema);
