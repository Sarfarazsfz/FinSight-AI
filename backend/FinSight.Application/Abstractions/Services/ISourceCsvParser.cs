using FinSight.Application.DTOs.Ingestion;

namespace FinSight.Application.Abstractions.Services;

public interface ISourceCsvParser
{
    Task<IReadOnlyList<PaymentCsvRow>> ParsePaymentsAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankCsvRow>> ParseBankAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementCsvRow>> ParseSettlementsAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}