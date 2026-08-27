namespace FinSight.Application.AI;

public interface IFinanceToolRegistry
{
    IReadOnlyCollection<string> ToolNames { get; }

    bool TryGet(
        string toolName,
        out IFinanceTool? tool);
}
