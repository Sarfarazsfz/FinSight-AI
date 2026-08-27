using System.Globalization;
using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Ingestion;

public sealed class BatchIngestionService : IBatchIngestionService
{
    private readonly ISourceCsvParser _csvParser;
    private readonly IBatchIngestionValidator _validator;
    private readonly IBatchRepository _batchRepository;
    private readonly IPaymentRecordRepository _paymentRepository;
    private readonly IBankRecordRepository _bankRepository;
    private readonly ISettlementRecordRepository _settlementRepository;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IUnitOfWork _unitOfWork;

    public BatchIngestionService(
        ISourceCsvParser csvParser,
        IBatchIngestionValidator validator,
        IBatchRepository batchRepository,
        IPaymentRecordRepository paymentRepository,
        IBankRecordRepository bankRepository,
        ISettlementRecordRepository settlementRepository,
        IAuditLogWriter auditLogWriter,
        IUnitOfWork unitOfWork)
    {
        _csvParser = csvParser;
        _validator = validator;
        _batchRepository = batchRepository;
        _paymentRepository = paymentRepository;
        _bankRepository = bankRepository;
        _settlementRepository = settlementRepository;
        _auditLogWriter = auditLogWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task<BatchIngestionResult> IngestAsync(
        BatchIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var paymentRows = await _csvParser.ParsePaymentsAsync(
            request.PaymentFile,
            cancellationToken);

        var bankRows = await _csvParser.ParseBankAsync(
            request.BankFile,
            cancellationToken);

        var settlementRows = await _csvParser.ParseSettlementsAsync(
            request.SettlementFile,
            cancellationToken);

        var paymentValidation =
            _validator.ValidatePayments(paymentRows);

        var bankValidation =
            _validator.ValidateBank(bankRows);

        var settlementValidation =
            _validator.ValidateSettlements(settlementRows);

        var validationErrors =
            paymentValidation.Errors
                .Concat(bankValidation.Errors)
                .Concat(settlementValidation.Errors)
                .ToList();

        if (validationErrors.Count > 0)
        {
            var message = string.Join(
                Environment.NewLine,
                validationErrors.Select(error =>
                    $"{error.Source} row {error.RowNumber}: " +
                    $"{error.Field} - {error.Message}"));

            throw new InvalidDataException(
                $"Batch validation failed:{Environment.NewLine}{message}");
        }

        var batch = new Batch(
            request.BatchLabel,
            paymentRows.Count,
            bankRows.Count,
            settlementRows.Count,
            validationStatus: "Valid",
            createdBy: request.CreatedBy);

        await _batchRepository.AddAsync(
            batch,
            cancellationToken);

        var paymentRecords = paymentRows
            .Select(row => CreatePaymentRecord(batch.Id, row))
            .ToList();

        var bankRecords = bankRows
            .Select(row => CreateBankRecord(batch.Id, row))
            .ToList();

        var settlementRecords = settlementRows
            .Select(row => CreateSettlementRecord(batch.Id, row))
            .ToList();

        await _paymentRepository.AddRangeAsync(
            paymentRecords,
            cancellationToken);

        await _bankRepository.AddRangeAsync(
            bankRecords,
            cancellationToken);

        await _settlementRepository.AddRangeAsync(
            settlementRecords,
            cancellationToken);

        var batchPayload = JsonSerializer.Serialize(
            new
            {
                batch_id = batch.Id,
                batch_label = batch.BatchLabel,
                created_by = batch.CreatedBy,
                validation_status = batch.ValidationStatus,
                payment_record_count = paymentRecords.Count,
                bank_record_count = bankRecords.Count,
                settlement_record_count = settlementRecords.Count
            });

        await _auditLogWriter.AddAsync(
            new AuditLog(
                AuditEventType.BatchCreated,
                batchPayload,
                relatedEntityType: "Batch",
                relatedEntityId: batch.Id),
            cancellationToken);

        var validationPayload = JsonSerializer.Serialize(
            new
            {
                batch_id = batch.Id,
                validation_status = batch.ValidationStatus,
                payment_record_count = paymentRecords.Count,
                bank_record_count = bankRecords.Count,
                settlement_record_count = settlementRecords.Count,
                total_record_count =
                    paymentRecords.Count +
                    bankRecords.Count +
                    settlementRecords.Count
            });

        await _auditLogWriter.AddAsync(
            new AuditLog(
                AuditEventType.BatchValidated,
                validationPayload,
                relatedEntityType: "Batch",
                relatedEntityId: batch.Id),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new BatchIngestionResult
        {
            BatchId = batch.Id,
            ValidationStatus = batch.ValidationStatus,
            PaymentRecordCount = paymentRecords.Count,
            BankRecordCount = bankRecords.Count,
            SettlementRecordCount = settlementRecords.Count,
            TotalRecordCount =
                paymentRecords.Count +
                bankRecords.Count +
                settlementRecords.Count
        };
    }

    private static PaymentRecord CreatePaymentRecord(
        Guid batchId,
        PaymentCsvRow row)
    {
        return new PaymentRecord(
            batchId,
            row.PaymentRecordId.Trim(),
            row.TransactionReference.Trim(),
            ParseAmount(row.Amount),
            row.Currency.Trim().ToUpperInvariant(),
            ParseDate(row.TransactionDate),
            row.PaymentStatus.Trim().ToUpperInvariant());
    }

    private static BankRecord CreateBankRecord(
        Guid batchId,
        BankCsvRow row)
    {
        return new BankRecord(
            batchId,
            row.BankRecordId.Trim(),
            row.TransactionReference.Trim(),
            ParseAmount(row.Amount),
            row.Currency.Trim().ToUpperInvariant(),
            ParseDate(row.TransactionDate),
            row.BankStatus.Trim().ToUpperInvariant());
    }

    private static SettlementRecord CreateSettlementRecord(
        Guid batchId,
        SettlementCsvRow row)
    {
        return new SettlementRecord(
            batchId,
            row.SettlementRecordId.Trim(),
            row.TransactionReference.Trim(),
            ParseAmount(row.Amount),
            row.Currency.Trim().ToUpperInvariant(),
            ParseDate(row.TransactionDate),
            row.SettlementStatus.Trim().ToUpperInvariant());
    }

    private static decimal ParseAmount(string value)
    {
        if (!decimal.TryParse(
                value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            throw new InvalidDataException(
                $"Invalid decimal amount: '{value}'.");
        }

        return decimal.Round(amount, 2);
    }

    private static DateOnly ParseDate(string value)
    {
        if (!DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new InvalidDataException(
                $"Invalid transaction date: '{value}'.");
        }

        return date;
    }
}