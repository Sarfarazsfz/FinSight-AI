using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Ingestion;

namespace FinSight.Tests.Ingestion;

/// <summary>
/// Phase 4A.3 (Structured Batch Validation Errors): proves
/// BatchIngestionService.IngestAsync still throws a plain
/// InvalidDataException (preserving the pre-existing service-layer
/// contract -- see ReconciliationPipelineIntegrationTests
/// .InvalidBatch_IsRejected_AndPersistsNothing) when validation fails, but
/// now carries the structured IngestionValidationError list via
/// ex.Data["Errors"], alongside the unchanged joined-string message --
/// and that valid input still completes normally without throwing it.
/// Every dependency is faked (no database), so this exercises only the
/// throw-site change, not CSV parsing or validation rules themselves.
/// </summary>
[TestFixture]
public sealed class BatchIngestionServiceValidationTests
{
    [Test]
    public void IngestAsync_WithPaymentValidationErrors_ThrowsInvalidDataExceptionWithMatchingDataErrorsAndMessage()
    {
        var paymentErrors = new List<IngestionValidationError>
        {
            new()
            {
                Source = "Payment",
                RowNumber = 2,
                Field = "payment_record_id",
                Message = "Required value is missing."
            }
        };

        var service = CreateService(paymentErrors: paymentErrors);
        var request = CreateRequest();

        var ex =
            Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await service.IngestAsync(
                        request,
                        CancellationToken.None));

        var expectedMessage =
            "Batch validation failed:" + Environment.NewLine +
            "Payment row 2: payment_record_id - Required value is missing.";

        Assert.Multiple(() =>
        {
            Assert.That(
                ex!.Data["Errors"],
                Is.EqualTo(paymentErrors));

            Assert.That(ex.Message, Is.EqualTo(expectedMessage));
        });
    }

    [Test]
    public void IngestAsync_WithErrorsFromAllThreeSources_PreservesPaymentThenBankThenSettlementOrderInData()
    {
        var paymentErrors = new List<IngestionValidationError>
        {
            new()
            {
                Source = "Payment",
                RowNumber = 2,
                Field = "amount",
                Message = "Amount must be greater than zero."
            }
        };

        var bankErrors = new List<IngestionValidationError>
        {
            new()
            {
                Source = "Bank",
                RowNumber = 3,
                Field = "bank_record_id",
                Message = "Must match BANK-000001 style."
            }
        };

        var settlementErrors = new List<IngestionValidationError>
        {
            new()
            {
                Source = "Settlement",
                RowNumber = 4,
                Field = "currency",
                Message = "Currency must be INR."
            }
        };

        var service = CreateService(
            paymentErrors: paymentErrors,
            bankErrors: bankErrors,
            settlementErrors: settlementErrors);

        var request = CreateRequest();

        var ex =
            Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await service.IngestAsync(
                        request,
                        CancellationToken.None));

        var expectedOrder =
            paymentErrors
                .Concat(bankErrors)
                .Concat(settlementErrors)
                .ToList();

        Assert.That(ex!.Data["Errors"], Is.EqualTo(expectedOrder));
    }

    [Test]
    public void IngestAsync_WithNoValidationErrors_DoesNotThrowBatchValidationException()
    {
        var service = CreateService();
        var request = CreateRequest();

        BatchIngestionResult? result = null;

        Assert.DoesNotThrowAsync(
            async () =>
                result = await service.IngestAsync(
                    request,
                    CancellationToken.None));

        Assert.That(result, Is.Not.Null);
    }

    private static BatchIngestionRequest CreateRequest()
    {
        return new BatchIngestionRequest
        {
            BatchLabel = "Validation Test Batch",
            CreatedBy = "unit-test",
            PaymentFile = new MemoryStream(),
            BankFile = new MemoryStream(),
            SettlementFile = new MemoryStream()
        };
    }

    private static BatchIngestionService CreateService(
        IReadOnlyList<IngestionValidationError>? paymentErrors = null,
        IReadOnlyList<IngestionValidationError>? bankErrors = null,
        IReadOnlyList<IngestionValidationError>? settlementErrors = null)
    {
        return new BatchIngestionService(
            new FakeSourceCsvParser(),
            new FakeBatchIngestionValidator(
                paymentErrors ?? Array.Empty<IngestionValidationError>(),
                bankErrors ?? Array.Empty<IngestionValidationError>(),
                settlementErrors ?? Array.Empty<IngestionValidationError>()),
            new FakeBatchRepository(),
            new FakePaymentRecordRepository(),
            new FakeBankRecordRepository(),
            new FakeSettlementRecordRepository(),
            new FakeAuditLogWriter(),
            new FakeUnitOfWork());
    }

    private sealed class FakeSourceCsvParser : ISourceCsvParser
    {
        public Task<IReadOnlyList<PaymentCsvRow>> ParsePaymentsAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PaymentCsvRow>>(
                new List<PaymentCsvRow>
                {
                    new()
                    {
                        PaymentRecordId = "PAY-000001",
                        TransactionReference = "TXN-0001",
                        Amount = "100.00",
                        Currency = "INR",
                        TransactionDate = "2026-01-01",
                        PaymentStatus = "SUCCESS"
                    }
                });
        }

        public Task<IReadOnlyList<BankCsvRow>> ParseBankAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BankCsvRow>>(
                new List<BankCsvRow>
                {
                    new()
                    {
                        BankRecordId = "BANK-000001",
                        TransactionReference = "TXN-0001",
                        Amount = "100.00",
                        Currency = "INR",
                        TransactionDate = "2026-01-01",
                        BankStatus = "SUCCESS"
                    }
                });
        }

        public Task<IReadOnlyList<SettlementCsvRow>> ParseSettlementsAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SettlementCsvRow>>(
                new List<SettlementCsvRow>
                {
                    new()
                    {
                        SettlementRecordId = "SET-000001",
                        TransactionReference = "TXN-0001",
                        Amount = "100.00",
                        Currency = "INR",
                        TransactionDate = "2026-01-01",
                        SettlementStatus = "SETTLED"
                    }
                });
        }
    }

    private sealed class FakeBatchIngestionValidator : IBatchIngestionValidator
    {
        private readonly IReadOnlyList<IngestionValidationError> _paymentErrors;
        private readonly IReadOnlyList<IngestionValidationError> _bankErrors;
        private readonly IReadOnlyList<IngestionValidationError> _settlementErrors;

        public FakeBatchIngestionValidator(
            IReadOnlyList<IngestionValidationError> paymentErrors,
            IReadOnlyList<IngestionValidationError> bankErrors,
            IReadOnlyList<IngestionValidationError> settlementErrors)
        {
            _paymentErrors = paymentErrors;
            _bankErrors = bankErrors;
            _settlementErrors = settlementErrors;
        }

        public BatchIngestionValidationResult ValidatePayments(
            IReadOnlyList<PaymentCsvRow> rows)
        {
            return new BatchIngestionValidationResult { Errors = _paymentErrors };
        }

        public BatchIngestionValidationResult ValidateBank(
            IReadOnlyList<BankCsvRow> rows)
        {
            return new BatchIngestionValidationResult { Errors = _bankErrors };
        }

        public BatchIngestionValidationResult ValidateSettlements(
            IReadOnlyList<SettlementCsvRow> rows)
        {
            return new BatchIngestionValidationResult { Errors = _settlementErrors };
        }
    }

    private sealed class FakeBatchRepository : IBatchRepository
    {
        public Task AddAsync(
            Batch batch,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Batch?> GetByIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageByOwnerAsync(
            Guid ownerUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakePaymentRecordRepository : IPaymentRecordRepository
    {
        public Task AddRangeAsync(
            IReadOnlyCollection<PaymentRecord> records,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PaymentRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PaymentRecord?> GetByIdAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeBankRecordRepository : IBankRecordRepository
    {
        public Task AddRangeAsync(
            IReadOnlyCollection<BankRecord> records,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BankRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BankRecord?> GetByIdAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeSettlementRecordRepository : ISettlementRecordRepository
    {
        public Task AddRangeAsync(
            IReadOnlyCollection<SettlementRecord> records,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SettlementRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SettlementRecord?> GetByIdAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public Task AddAsync(
            AuditLog auditLog,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<AuditLog> auditLogs,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
