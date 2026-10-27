using Jobly.Api.Common;
using Jobly.Api.Features.Cvs;

namespace Jobly.Api.Tests.Fakes;

/// <summary>An <see cref="ICvTextExtractor"/> that returns whatever the test says, so no real PDF or DOCX is needed.</summary>
public sealed class FakeCvTextExtractor : ICvTextExtractor
{
    /// <summary>What the next call returns. Set it to a failure to test the error path.</summary>
    public Result<string> Result { get; set; } = Jobly.Api.Common.Result.Success("Text read from the file");

    public Task<Result<string>> Extract(Stream content, string fileName, CancellationToken cancellationToken) => Task.FromResult(Result);
}
