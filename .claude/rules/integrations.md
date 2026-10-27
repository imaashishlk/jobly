# Integrations

An **integration** is anything Jobly talks to outside its own code and database: the LLM provider, an email service, a job board API, file storage, and so on.

**Goal:** everything about one integration is easy to find, and any integration can be replaced or faked without touching business logic.

## Folder layout

```
src/Jobly.Api/
  Integrations/
    Llm/                                 Everything about the LLM, in one folder
      ILlmClient.cs                      The contract: what the app needs from an LLM
      LlmRequest.cs / LlmResponse.cs
      LlmOptions.cs                      Settings (model, base URL, timeout), bound from configuration
      <Provider>LlmClient.cs             The real implementation, using a typed HttpClient (OllamaLlmClient)
      LlmErrors.cs                       Error codes this integration can return
      LlmServiceCollectionExtensions.cs  AddLlmIntegration(): the only DI registration
    Email/
      ...
```

The contract and the implementation sit side by side. Features depend only on the interface; they never mention the provider class.
- The **interface** says *what* the app needs ("give me a completion for this prompt").
- The **implementation** says *how* ("call this provider's HTTP API with this key").

**Why this still keeps providers swappable:** features only receive `ILlmClient` through dependency injection. Changing provider, or using a fake in tests, means changing one registration, not a single feature. The implementation class is `internal`, so a feature can't use it by accident.

## Rules for every integration

### One folder per integration. Nothing about it lives anywhere else.
**Why:** To understand, debug, or replace the LLM integration you open one folder. Nothing is scattered across the codebase.

### Business code depends only on the interface (`ILlmClient`), never on the provider class.
**Why:** Loose coupling. Changing provider, or adding a second one, means writing a new class in `Integrations/Llm/`. Features, endpoints, and tests don't change.

### Each integration registers itself with one extension method (`AddLlmIntegration()`), called from `Program.cs`.
**Why:** `Program.cs` stays a readable list of features. Turning an integration on, off, or swapping it is one line.

### Settings come from an `Options` class bound to the `Integrations:<Name>` section of configuration.
**Why:**
- Settings are typed and validated at startup.
- Missing configuration fails fast with a clear error, not halfway through a user's request.
- Secrets such as API keys come from user secrets locally and environment variables in Azure, never from `appsettings.json`.

### HTTP integrations use a typed client from `IHttpClientFactory`, with an explicit timeout.
**Why:** See [code-style.md](code-style.md). It also means base URL, headers, and timeout are configured in one place, and every call is traced automatically.

### Integrations return `Result<T>`, not exceptions, for expected failures (timeout, rate limit, provider error).
**Why:** Callers have to handle "the LLM is unavailable" explicitly, for example by showing a friendly message. It can't surface as an unhandled crash.

### Every integration method takes a `CancellationToken`.
**Why:** LLM calls are slow and cost money per token. If the user leaves, the call should stop.

### Every integration logs its calls: name, duration, outcome, and for the LLM, the model and token counts.
**Why:** External calls are where most slowness, cost, and failures come from. See [logging.md](logging.md).

### Tests never call the real service.
Unit tests use a fake `ILlmClient`. Integration tests replace the integration in DI.
**Why:** Tests stay fast, free, repeatable, and runnable without network access or API keys.
