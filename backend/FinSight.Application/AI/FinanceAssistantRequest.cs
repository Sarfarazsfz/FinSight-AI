namespace FinSight.Application.AI;

public sealed class FinanceAssistantRequest
{
    public Guid RunId { get; init; }

    public string Question { get; init; } = string.Empty;
}
