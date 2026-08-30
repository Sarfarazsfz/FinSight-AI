using Google.GenAI;
using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public sealed class GeminiFinanceAssistantModelClient
    : IFinanceAssistantModelClient
{
    private readonly string _apiKey;
    private readonly Lazy<Client?> _client;

    // Deliberately LAZY validation (was an eager throw in this class's DI
    // registration factory before the Global AI Provider DI Resolution
    // fix): Gemini is one of potentially several configured providers in
    // AiProviderOptions.FinanceAssistant.ProviderOrder. The old eager
    // throw fired the instant this client was resolved -- which happened
    // merely because GeminiFinanceAssistantProvider was a constructor
    // dependency of FinanceAssistantProviderRouter, regardless of whether
    // Gemini was actually configured or even present in the order. Now
    // the "not configured" failure surfaces only when the client is
    // actually asked to generate content, exactly like
    // OpenAiFinanceAssistantProvider/NvidiaFinanceAssistantProvider's own
    // Lazy<ChatClient?> pattern -- letting ProviderFallbackChain treat it
    // as an ordinary per-candidate failure and fall through, instead of
    // crashing router construction outright.
    public GeminiFinanceAssistantModelClient(
        string apiKey)
    {
        _apiKey = apiKey ?? string.Empty;

        _client =
            new Lazy<Client?>(
                () =>
                    string.IsNullOrWhiteSpace(_apiKey)
                        ? null
                        : new Client(apiKey: _apiKey));
    }

    public Task<GenerateContentResponse> GenerateContentAsync(
        string model,
        List<Content> contents,
        GenerateContentConfig config,
        CancellationToken cancellationToken = default)
    {
        var client = _client.Value;

        if (client is null)
        {
            throw new InvalidOperationException(
                "Gemini Finance Assistant provider is not configured.");
        }

        return client.Models.GenerateContentAsync(
            model,
            contents,
            config,
            cancellationToken);
    }
}
