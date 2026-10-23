using Jobly.Application.Cvs;
using Jobly.Domain.Common;

namespace Jobly.Application.Tests.Fakes;

/// <summary>An <see cref="ICvTextExtractor"/> that returns whatever the test says, so no real PDF or DOCX is needed.</summary>
public sealed class FakeCvTextExtractor : ICvTextExtractor
{
    /// <summary>What the next call returns. Set it to a failure to test the error path.</summary>
    public Result<string> Result { get; set; } = Domain.Common.Result.Success("Extracted CV text");

    /// <summary>How many times the extractor was called. Zero proves the service refused the file before reading it.</summary>
    public int CallCount { get; private set; }

    public Task<Result<string>> Extract(Stream content, string fileName, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
