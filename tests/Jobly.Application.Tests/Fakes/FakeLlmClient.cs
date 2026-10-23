using Jobly.Application.Integrations.Llm;
using Jobly.Domain.Common;

namespace Jobly.Application.Tests.Fakes;

/// <summary>
/// An <see cref="ILlmClient"/> that answers with whatever the test says. No model, no network, no cost.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    /// <summary>What the next call returns. Set <see cref="AnswerText"/> for a normal answer, or set this to a failure.</summary>
    public Result<LlmResponse> Result { get; set; } = Domain.Common.Result.Success(new LlmResponse(ValidAnswer, "fake-model", 100, 50));

    /// <summary>A well-formed answer in the shape the Match agent asks for.</summary>
    public const string ValidAnswer = """{"score":72,"summary":"Good fit for the backend work.","matchingSkills":["C#","SQL"],"gaps":["Kubernetes"]}""";

    /// <summary>The last request the code under test sent, so a test can check what was asked.</summary>
    public LlmRequest? LastRequest { get; private set; }

    public int CallCount { get; private set; }

    /// <summary>Shortcut: the model answered successfully with this text.</summary>
    public void AnswerWith(string text) => Result = Domain.Common.Result.Success(new LlmResponse(text, "fake-model", 100, 50));

    public Task<Result<LlmResponse>> Complete(LlmRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(Result);
    }
}
