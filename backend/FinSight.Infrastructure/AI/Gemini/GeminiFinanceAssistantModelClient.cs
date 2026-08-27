using Google.GenAI;
using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public sealed class GeminiFinanceAssistantModelClient
    : IFinanceAssistantModelClient
{
    private readonly Client _client;

    public GeminiFinanceAssistantModelClient(
        Client client)
    {
        _client = client;
    }

    public Task<GenerateContentResponse> GenerateContentAsync(
        string model,
        List<Content> contents,
        GenerateContentConfig config,
        CancellationToken cancellationToken = default)
    {
        return _client.Models.GenerateContentAsync(
            model,
            contents,
            config,
            cancellationToken);
    }
}
