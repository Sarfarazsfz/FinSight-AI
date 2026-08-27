namespace FinSight.Application.AI;

public sealed class FinanceToolRegistry
    : IFinanceToolRegistry
{
    private readonly IReadOnlyDictionary<string, IFinanceTool> _tools;

    public FinanceToolRegistry(
        IEnumerable<IFinanceTool> tools)
    {
        _tools =
            tools.ToDictionary(
                x => x.Name,
                x => x,
                StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> ToolNames =>
        _tools.Keys.ToArray();

    public bool TryGet(
        string toolName,
        out IFinanceTool? tool)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            tool = null;
            return false;
        }

        return _tools.TryGetValue(
            toolName,
            out tool);
    }
}
