using Jobly.Api.Common;
using Jobly.Api.Integrations.Llm;

namespace Jobly.Api.Tests.Fakes;

/// <summary>An <see cref="ILlmClient"/> that answers with whatever the test says. No model, no network, no cost.</summary>
public sealed class FakeLlmClient : ILlmClient
{
    /// <summary>A well-formed answer in the shape the Match agent asks for.</summary>
    public const string ValidAnswer = """{"score":72,"summary":"Good fit for the backend work.","matchingSkills":["C#","SQL"],"gaps":["Kubernetes"]}""";

    /// <summary>What the next call returns. Set it to a failure to test the "model is down" path.</summary>
    public Result<LlmResponse> Result { get; set; } = Jobly.Api.Common.Result.Success(new LlmResponse(ValidAnswer, "fake-model", 100, 50));

    public int CallCount { get; private set; }

    public void AnswerWith(string text) => Result = Jobly.Api.Common.Result.Success(new LlmResponse(text, "fake-model", 100, 50));

    public Task<Result<LlmResponse>> Complete(LlmRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
