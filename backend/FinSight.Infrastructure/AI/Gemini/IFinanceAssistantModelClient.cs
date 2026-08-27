using Google.GenAI.Types;

namespace FinSight.Infrastructure.AI.Gemini;

public interface IFinanceAssistantModelClient
{
    Task<GenerateContentResponse> GenerateContentAsync(
        string model,
        List<Content> contents,
        GenerateContentConfig config,
        CancellationToken cancellationToken = default);
}
