namespace FinSight.Application.DTOs.Ingestion;

public sealed class IngestionValidationError
{
    public string Source { get; init; } = string.Empty;

    public int? RowNumber { get; init; }

    public string Field { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}