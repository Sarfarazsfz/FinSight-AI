namespace FinSight.Application.AI;

public sealed class FinanceToolParameter
{
    public string Type { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Required { get; init; }
}
