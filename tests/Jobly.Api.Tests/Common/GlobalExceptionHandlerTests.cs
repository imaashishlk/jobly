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

    [Fact]
    public async Task TryHandleAsync_RequestTooLarge_Writes413WithAMessageAboutTheSize()
    {
        // Arrange: Kestrel throws this when an upload is bigger than the request size limit.
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            services.GetRequiredService<IProblemDetailsService>());
        var exception = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        // Assert: keeps the status the framework chose (413), and tells the user what to do.
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, httpContext.Response.StatusCode);

        httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(413, json.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("too large", json.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_BadHttpRequest_Writes400ProblemInsteadOf500()
    {
        // Arrange: ASP.NET Core throws this when the caller sends something it can't read, for example ?status=Banana.
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            services.GetRequiredService<IProblemDetailsService>());
        var exception = new BadHttpRequestException("Failed to bind parameter from the query string");

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, TestContext.Current.CancellationToken);

        // Assert: the caller made the mistake, so 400, and the framework's message stays out of the response.
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);

        httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("Failed to bind", body, StringComparison.Ordinal);
    }
}
