namespace FinSight.Application.AI;

public interface IFinanceTool
{
    string Name { get; }

    Task<FinanceToolResult> ExecuteAsync(
        FinanceToolRequest request,
        CancellationToken cancellationToken = default);
}
