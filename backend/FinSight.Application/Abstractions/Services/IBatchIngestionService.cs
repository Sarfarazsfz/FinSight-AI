using FinSight.Application.DTOs.Ingestion;

namespace FinSight.Application.Abstractions.Services;

public interface IBatchIngestionService
{
    Task<BatchIngestionResult> IngestAsync(
        BatchIngestionRequest request,
        CancellationToken cancellationToken = default);
}