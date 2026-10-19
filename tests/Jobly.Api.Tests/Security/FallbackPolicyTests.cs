using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jobly.Api.Tests.Security;

public sealed class FallbackPolicyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void FallbackPolicy_AppStarted_RequiresAuthenticatedUser()
    {
        // Arrange: read the authorization settings the real app registered.
        var options = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        // Assert: a fallback policy exists, and it denies anonymous users. So any endpoint that forgets
        // .RequireAuthorization() / .AllowAnonymous() is closed by default (see .claude/rules/security.md).
        Assert.NotNull(options.FallbackPolicy);
        Assert.Contains(options.FallbackPolicy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}
