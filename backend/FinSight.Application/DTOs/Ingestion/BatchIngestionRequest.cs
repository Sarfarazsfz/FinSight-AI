namespace FinSight.Application.DTOs.Ingestion;

public sealed class BatchIngestionRequest
{
    public required string BatchLabel { get; init; }

    public required string CreatedBy { get; init; }

    public required Stream PaymentFile { get; init; }

    public required Stream BankFile { get; init; }

    public required Stream SettlementFile { get; init; }
}