namespace FinSight.Application.AI;

public sealed class FinanceToolResult
{
    public string ToolName { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string DataJson { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}
