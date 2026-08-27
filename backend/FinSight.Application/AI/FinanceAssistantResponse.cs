namespace FinSight.Application.AI;

public sealed class FinanceAssistantResponse
{
    public string Answer { get; init; } = string.Empty;

    public IReadOnlyList<string> ToolsUsed { get; init; }
        = Array.Empty<string>();

    public string? TraceId { get; init; }
}
