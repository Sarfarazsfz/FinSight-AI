using FinSight.Application.DTOs.Ingestion;

namespace FinSight.Application.Abstractions.Services;

public interface IBatchIngestionValidator
{
    BatchIngestionValidationResult ValidatePayments(
        IReadOnlyList<PaymentCsvRow> rows);

    BatchIngestionValidationResult ValidateBank(
        IReadOnlyList<BankCsvRow> rows);

    BatchIngestionValidationResult ValidateSettlements(
        IReadOnlyList<SettlementCsvRow> rows);
}