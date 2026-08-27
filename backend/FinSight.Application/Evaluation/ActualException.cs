namespace FinSight.Application.Evaluation;

public sealed class ActualException
{
    public Guid ExceptionId { get; set; }

    public Guid RunId { get; set; }

    public Guid ReconciliationResultId { get; set; }

    public string Category { get; set; } =
        string.Empty;

    public string TransactionReference { get; set; } =
        string.Empty;
}
