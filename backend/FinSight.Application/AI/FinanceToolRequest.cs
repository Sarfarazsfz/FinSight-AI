namespace FinSight.Application.AI;

public sealed class FinanceToolRequest
{
    public Guid? RunId { get; init; }

    public Guid? ExceptionId { get; init; }

    public Guid? ResultId { get; init; }

    public string? TransactionReference { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
