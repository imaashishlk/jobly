using Jobly.Application.Integrations.Llm;
using Jobly.Infrastructure.Integrations.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jobly.Infrastructure.Tests.Integrations.Llm;

public sealed class LlmServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmIntegration(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddLlmIntegration_ValidSettings_ResolvesTheClientAndBindsTheOptions()
    {
        // Arrange
        using var provider = BuildProvider(new()
        {
            ["Integrations:Llm:BaseUrl"] = "http://localhost:11434",
            ["Integrations:Llm:ChatModel"] = "qwen2.5:7b",
            ["Integrations:Llm:EmbeddingModel"] = "bge-m3",
        });

        // Act
        var client = provider.GetRequiredService<ILlmClient>();
        var options = provider.GetRequiredService<IOptions<LlmOptions>>().Value;

        // Assert: business code asks for the interface and gets the Ollama class.
        Assert.IsType<OllamaLlmClient>(client);
        Assert.Equal("qwen2.5:7b", options.ChatModel);
        Assert.Equal(300, options.ChatTimeoutSeconds);
    }

    [Fact]
    public void AddLlmIntegration_ChatModelMissing_FailsWithAClearValidationError()
    {
        // Arrange
        using var provider = BuildProvider(new()
        {
            ["Integrations:Llm:BaseUrl"] = "http://localhost:11434",
            ["Integrations:Llm:EmbeddingModel"] = "bge-m3",
        });

        // Act and Assert: the problem shows up when the settings are first read, not halfway through a user's request.
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LlmOptions>>().Value);
    }

    [Fact]
    public void AddLlmIntegration_BaseUrlIsNotAUrl_FailsValidation()
    {
        // Arrange
        using var provider = BuildProvider(new()
        {
            ["Integrations:Llm:BaseUrl"] = "not a url",
            ["Integrations:Llm:ChatModel"] = "qwen2.5:7b",
            ["Integrations:Llm:EmbeddingModel"] = "bge-m3",
        });

        // Act and Assert
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LlmOptions>>().Value);
    }
}
