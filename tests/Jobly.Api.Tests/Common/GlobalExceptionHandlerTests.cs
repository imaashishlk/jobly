using System.Text.Json;
using Jobly.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobly.Api.Tests.Common;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_UnhandledException_Writes500ProblemWithoutExceptionDetails()
    {
        // Arrange: a fake HTTP request with the real ProblemDetails services behind it.
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            services.GetRequiredService<IProblemDetailsService>());
        var exception = new InvalidOperationException("Internal detail that must not reach the caller");

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        // Assert: handled, status 500, a ProblemDetails body with a traceId, and no exception text leaked.
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("Internal detail", body, StringComparison.Ordinal);
    }
}
