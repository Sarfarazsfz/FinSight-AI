namespace FinSight.Application.AI;

public sealed class FinanceAssistantProviderResponse
{
    public string Answer { get; init; } = string.Empty;

    public IReadOnlyList<FinanceToolCall> ToolCalls { get; init; }
        = Array.Empty<FinanceToolCall>();

    public bool RequiresToolExecution { get; init; }
}
