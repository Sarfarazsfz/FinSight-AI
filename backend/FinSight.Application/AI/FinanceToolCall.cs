using System.Text.Json;

namespace FinSight.Application.AI;

public sealed class FinanceToolCall
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, JsonElement> Arguments { get; init; }
        = new Dictionary<string, JsonElement>();

    public string? Id { get; init; }

    public string? ModelPartJson { get; init; }
}
