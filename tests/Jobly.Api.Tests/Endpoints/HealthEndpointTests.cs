using System.Net;

namespace Jobly.Api.Tests.Endpoints;

// IClassFixture: xUnit creates one in-memory API (JoblyApiFactory) and shares it across the tests in this class.
public sealed class HealthEndpointTests(JoblyApiFactory factory) : IClassFixture<JoblyApiFactory>
{
    [Fact]
    public async Task GetHealth_WithoutSignIn_ReturnsOkHealthy()
    {
        // Arrange: a client with no sign-in at all.
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert: public on purpose, so Docker and Azure can check the app is alive.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
