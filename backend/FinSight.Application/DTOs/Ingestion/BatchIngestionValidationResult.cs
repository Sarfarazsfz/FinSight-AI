namespace FinSight.Application.DTOs.Ingestion;

public sealed class BatchIngestionValidationResult
{
    public bool IsValid =>
        Errors.Count == 0;

    public IReadOnlyList<IngestionValidationError> Errors { get; init; }
        = Array.Empty<IngestionValidationError>();
}