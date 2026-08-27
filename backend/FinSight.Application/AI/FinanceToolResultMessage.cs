namespace FinSight.Application.AI;

public sealed class FinanceToolResultMessage
{
    public string? ToolCallId { get; init; }

    public string ToolName { get; init; } = string.Empty;

    public string ResultJson { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public bool Success { get; init; }
}
