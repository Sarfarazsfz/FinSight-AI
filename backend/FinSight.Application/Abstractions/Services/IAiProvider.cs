using FinSight.Application.DTOs.Ai;

namespace FinSight.Application.Abstractions.Services;

public interface IAiProvider
{
    string ProviderName { get; }

    bool IsAvailable { get; }

    Task<AiExplanationResponse> GenerateExplanationAsync(
        AiExplanationRequest request,
        CancellationToken cancellationToken = default);
}