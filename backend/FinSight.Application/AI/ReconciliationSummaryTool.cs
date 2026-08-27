using System.Text.Json;
using FinSight.Application.Abstractions.Reconciliation;

namespace FinSight.Application.AI;

public sealed class ReconciliationSummaryTool
    : IReconciliationSummaryTool
{
    public string Name =>
        "getReconciliationSummary";

    private readonly IReconciliationSummaryBuilder _summaryBuilder;

    public ReconciliationSummaryTool(
        IReconciliationSummaryBuilder summaryBuilder)
    {
        _summaryBuilder = summaryBuilder;
    }

    public async Task<FinanceToolResult> ExecuteAsync(
        FinanceToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RunId is null ||
            request.RunId == Guid.Empty)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "INVALID_ARGUMENT",
                ErrorMessage = "A valid runId is required."
            };
        }

        var response =
            await _summaryBuilder.BuildAsync(
                request.RunId.Value,
                cancellationToken);

        if (response is null)
        {
            return new FinanceToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "RUN_NOT_FOUND",
                ErrorMessage =
                    $"Reconciliation run '{request.RunId.Value}' was not found."
            };
        }

        return new FinanceToolResult
        {
            ToolName = Name,
            Success = true,
            DataJson =
                JsonSerializer.Serialize(response)
        };
    }
}
