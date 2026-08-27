namespace FinSight.Application.AI;

public sealed class FinanceToolDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, FinanceToolParameter> Parameters { get; init; }
        = new Dictionary<string, FinanceToolParameter>();
}
