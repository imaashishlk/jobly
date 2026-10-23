using Jobly.Application.Integrations.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jobly.Infrastructure.Integrations.Llm;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registers the language model integration. Called once from Program.cs. To use another provider,
    /// write a new class that implements ILlmClient and register it here instead of the Ollama one.
    /// </summary>
    public static IServiceCollection AddLlmIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // Binds the Integrations:Llm section to LlmOptions, checks the [Required] / [Range] rules, and (ValidateOnStart)
        // does that when the app starts, so a wrong setting stops the app with a clear message instead of failing later.
        services.AddOptions<LlmOptions>()
            .Bind(configuration.GetSection(LlmOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // A typed client: IHttpClientFactory manages the connections, and requests carry the trace ID automatically.
        // Business code asks for ILlmClient and receives OllamaLlmClient with a ready HttpClient.
        services.AddHttpClient<ILlmClient, OllamaLlmClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);

            // The real per-call limit is applied inside the client. This is only a safety net a little above it.
            client.Timeout = TimeSpan.FromSeconds(settings.ChatTimeoutSeconds + 10);
        });

        return services;
    }
}
