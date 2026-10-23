namespace Jobly.Application.Integrations.Llm;

/// <summary>A model's answer, plus the numbers that matter for cost and speed.</summary>
/// <param name="Text">The answer text (JSON text when a schema was sent).</param>
/// <param name="Model">The name of the model that answered, kept so old results can be told apart after a model change.</param>
/// <param name="InputTokens">How many tokens the model read.</param>
/// <param name="OutputTokens">How many tokens the model wrote.</param>
public sealed record LlmResponse(string Text, string Model, int InputTokens, int OutputTokens);
