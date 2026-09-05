namespace FinSight.Application.DTOs.Ingestion;

public sealed class BatchIngestionRequest
{
    public required string BatchLabel { get; init; }

    public required string CreatedBy { get; init; }

    /// <summary>
    /// Optional so every pre-existing caller that constructs this DTO
    /// directly (unit/integration tests exercising ingestion in
    /// isolation) keeps compiling unchanged -- the resulting batch is
    /// simply unowned, matching how it already behaved before ownership
    /// existed. The real HTTP endpoint always sets this from the
    /// authenticated caller's identity, never from client input.
    /// </summary>
    public Guid? CreatedByUserId { get; init; }

    public required Stream PaymentFile { get; init; }

    public required Stream BankFile { get; init; }

    public required Stream SettlementFile { get; init; }
}