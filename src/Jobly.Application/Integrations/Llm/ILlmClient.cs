using Jobly.Domain.Common;

namespace Jobly.Application.Integrations.Llm;

/// <summary>
/// The one thing the app needs from a language model: answer a question. Business code (such as the Match agent)
/// depends only on this interface, so changing the provider means writing one new class and changing no service.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Asks the model. Expected failures come back as errors: <c>Llm.Unavailable</c> (503) when the model is down
    /// or too slow, <c>Llm.BadResponse</c> when its reply cannot be read.
    /// </summary>
    Task<Result<LlmResponse>> Complete(LlmRequest request, CancellationToken cancellationToken);
}
