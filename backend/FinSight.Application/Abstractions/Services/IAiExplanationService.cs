using FinSight.Application.DTOs.Ai;

namespace FinSight.Application.Abstractions.Services;

public interface IAiExplanationService
{
    Task<AiExplanationResponse> ExplainAsync(
        Guid exceptionId,
        CancellationToken cancellationToken = default);
}