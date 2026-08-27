namespace FinSight.Application.AI;

public sealed class FinanceAssistantProviderRequest
{
    public string Question { get; init; } = string.Empty;

    public Guid RunId { get; init; }

    public IReadOnlyCollection<FinanceToolDefinition> Tools { get; init; }
        = Array.Empty<FinanceToolDefinition>();

    public IReadOnlyCollection<FinanceToolCall> PreviousToolCalls { get; init; }
        = Array.Empty<FinanceToolCall>();

    public IReadOnlyCollection<FinanceToolResultMessage> ToolResults { get; init; }
        = Array.Empty<FinanceToolResultMessage>();
}
