using System.Net;
using System.Text;

namespace Jobly.Api.UnitTests.Fakes;

/// <summary>
/// Stands in for the network in tests: the code under test sends its HTTP request here, and the test decides
/// what comes back. Nothing real is ever called.
/// </summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    /// <summary>The path of the last request (for example <c>/api/chat</c>).</summary>
    public string? LastPath { get; private set; }

    /// <summary>The body of the last request, as text.</summary>
    public string? LastBody { get; private set; }

    public int CallCount { get; private set; }

    /// <summary>A handler that always answers with this status and JSON text.</summary>
    public static FakeHttpMessageHandler Replying(HttpStatusCode status, string json) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastPath = request.RequestUri?.AbsolutePath;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return await respond(request, cancellationToken);
    }
}
