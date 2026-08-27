namespace FinSight.Application.DTOs.Ai;

public sealed class AiExplanationResponse
{
    public string Provider { get; init; }
        = string.Empty;

    public string Explanation { get; init; }
        = string.Empty;

    public string? SuggestedCategory { get; init; }

    public DateTime GeneratedAtUtc { get; init; }
}