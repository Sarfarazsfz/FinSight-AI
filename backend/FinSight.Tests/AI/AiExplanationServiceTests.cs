using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.AI;

namespace FinSight.Tests.AI;

[TestFixture]
public sealed class AiExplanationServiceTests
{
    [Test]
    public async Task ExplainAsync_WithValidException_EnrichesException()
    {
        var exception =
            CreateException();

        var result =
            new ReconciliationResult(
                exception.RunId,
                Guid.NewGuid(),
                MatchStatus.Mismatched,
                ReconciliationReasonCode.AMOUNT_MISMATCH,
                "StrategyTwo_AmountDateToleranceMatch");

        var normalizedTransaction =
            CreateNormalizedTransaction(
                exception.RunId);

        var repository =
            new FakeReconciliationExceptionRepository(
                exception);

        var resultRepository =
            new FakeReconciliationResultRepository(
                result,
                exception.ReconciliationResultId);

        var normalizedRepository =
            new FakeNormalizedTransactionRepository(
                normalizedTransaction,
                result.NormalizedTransactionId);

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var provider =
            new FakeAiProvider(
                new AiExplanationResponse
                {
                    Provider =
                        "TestProvider",

                    Explanation =
                        "The payment and bank amounts differ by INR 10.",

                    SuggestedCategory =
                        "AmountMismatch",

                    GeneratedAtUtc =
                        DateTime.UtcNow
                });

        var service =
            new AiExplanationService(
                repository,
                resultRepository,
                normalizedRepository,
                auditWriter,
                unitOfWork,
                provider);

        var response =
            await service.ExplainAsync(
                exception.Id);

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Explanation,
                Is.EqualTo(
                    "The payment and bank amounts differ by INR 10."));

            Assert.That(
                response.SuggestedCategory,
                Is.EqualTo("AmountMismatch"));

            Assert.That(
                exception.AiExplanation,
                Is.EqualTo(
                    "The payment and bank amounts differ by INR 10."));

            Assert.That(
                exception.AiSuggestedCategory,
                Is.EqualTo("AmountMismatch"));

            Assert.That(
                exception.AiExplanationGeneratedAt,
                Is.Not.Null);

            Assert.That(
                exception.UpdatedAt,
                Is.Not.Null);

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));

            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(2));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(
                    AuditEventType.AiExplanationRequested));

            Assert.That(
                auditWriter.Events[1].EventType,
                Is.EqualTo(
                    AuditEventType.AiToolInvoked));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain(
                    "\"requested_provider\":\"TestProvider\""));

            Assert.That(
                auditWriter.Events[1].DetailPayload,
                Does.Contain(
                    "\"provider\":\"TestProvider\""));
        });
    }

    [Test]
    public void ExplainAsync_WithEmptyExceptionId_ThrowsArgumentException()
    {
        var service =
            CreateService(
                exception: null,
                result: null,
                resultLookupId: null,
                normalizedTransaction: null,
                normalizedTransactionLookupId: null);

        Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await service.ExplainAsync(
                    Guid.Empty));
    }

    [Test]
    public void ExplainAsync_WhenExceptionDoesNotExist_ThrowsKeyNotFound()
    {
        var service =
            CreateService(
                exception: null,
                result: null,
                resultLookupId: null,
                normalizedTransaction: null,
                normalizedTransactionLookupId: null);

        Assert.ThrowsAsync<KeyNotFoundException>(
            async () =>
                await service.ExplainAsync(
                    Guid.NewGuid()));
    }

    [Test]
    public void ExplainAsync_WhenResultDoesNotExist_ThrowsInvalidOperation()
    {
        var exception =
            CreateException();

        var service =
            CreateService(
                exception,
                result: null,
                resultLookupId: null,
                normalizedTransaction: null,
                normalizedTransactionLookupId: null);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.ExplainAsync(
                    exception.Id));
    }

    [Test]
    public void ExplainAsync_WhenResultBelongsToDifferentRun_ThrowsInvalidOperation()
    {
        var exception =
            CreateException();

        var result =
            new ReconciliationResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                MatchStatus.Matched,
                ReconciliationReasonCode.EXACT_MATCH,
                "StrategyOne_ExactReferenceMatch");

        var service =
            CreateService(
                exception,
                result,
                resultLookupId:
                    exception.ReconciliationResultId,
                normalizedTransaction: null,
                normalizedTransactionLookupId: null);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.ExplainAsync(
                    exception.Id));
    }

    [Test]
    public void ExplainAsync_WhenNormalizedTransactionDoesNotExist_ThrowsInvalidOperation()
    {
        var exception =
            CreateException();

        var result =
            new ReconciliationResult(
                exception.RunId,
                Guid.NewGuid(),
                MatchStatus.Mismatched,
                ReconciliationReasonCode.AMOUNT_MISMATCH,
                "StrategyTwo_AmountDateToleranceMatch");

        var service =
            CreateService(
                exception,
                result,
                resultLookupId:
                    exception.ReconciliationResultId,
                normalizedTransaction: null,
                normalizedTransactionLookupId:
                    result.NormalizedTransactionId);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.ExplainAsync(
                    exception.Id));
    }

    [Test]
    public void ExplainAsync_WhenNormalizedTransactionBelongsToDifferentRun_ThrowsInvalidOperation()
    {
        var exception =
            CreateException();

        var result =
            new ReconciliationResult(
                exception.RunId,
                Guid.NewGuid(),
                MatchStatus.Mismatched,
                ReconciliationReasonCode.AMOUNT_MISMATCH,
                "StrategyTwo_AmountDateToleranceMatch");

        var normalizedTransaction =
            CreateNormalizedTransaction(
                Guid.NewGuid());

        var service =
            CreateService(
                exception,
                result,
                resultLookupId:
                    exception.ReconciliationResultId,
                normalizedTransaction,
                normalizedTransactionLookupId:
                    result.NormalizedTransactionId);

        Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await service.ExplainAsync(
                    exception.Id));
    }

    [Test]
    public async Task ExplainAsync_WhenProviderFails_WritesFailureAuditAndRethrows()
    {
        var exception =
            CreateException();

        var result =
            new ReconciliationResult(
                exception.RunId,
                Guid.NewGuid(),
                MatchStatus.Mismatched,
                ReconciliationReasonCode.AMOUNT_MISMATCH,
                "StrategyTwo_AmountDateToleranceMatch");

        var normalizedTransaction =
            CreateNormalizedTransaction(
                exception.RunId);

        var auditWriter =
            new FakeAuditLogWriter();

        var unitOfWork =
            new FakeUnitOfWork();

        var provider =
            new FakeAiProvider(
                providerException:
                    new InvalidOperationException(
                        "Provider unavailable"));

        var service =
            new AiExplanationService(
                new FakeReconciliationExceptionRepository(
                    exception),
                new FakeReconciliationResultRepository(
                    result,
                    exception.ReconciliationResultId),
                new FakeNormalizedTransactionRepository(
                    normalizedTransaction,
                    result.NormalizedTransactionId),
                auditWriter,
                unitOfWork,
                provider);

        var thrown =
            Assert.ThrowsAsync<InvalidOperationException>(
                async () =>
                    await service.ExplainAsync(
                        exception.Id));

        Assert.Multiple(() =>
        {
            Assert.That(
                thrown!.Message,
                Is.EqualTo(
                    "Provider unavailable"));

            Assert.That(
                exception.AiExplanation,
                Is.Null);

            Assert.That(
                exception.AiSuggestedCategory,
                Is.Null);

            Assert.That(
                auditWriter.Events,
                Has.Count.EqualTo(1));

            Assert.That(
                auditWriter.Events[0].EventType,
                Is.EqualTo(
                    AuditEventType.AiExplanationFailed));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain(
                    "\"provider\":\"TestProvider\""));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain(
                    "\"error_type\":\"InvalidOperationException\""));

            Assert.That(
                auditWriter.Events[0].DetailPayload,
                Does.Contain(
                    "\"error_message\":\"Provider unavailable\""));

            Assert.That(
                unitOfWork.SaveChangesCalls,
                Is.EqualTo(1));
        });
    }

    private static AiExplanationService CreateService(
        ReconciliationException? exception,
        ReconciliationResult? result,
        Guid? resultLookupId,
        NormalizedTransaction? normalizedTransaction,
        Guid? normalizedTransactionLookupId)
    {
        return new AiExplanationService(
            new FakeReconciliationExceptionRepository(
                exception),

            new FakeReconciliationResultRepository(
                result,
                resultLookupId),

            new FakeNormalizedTransactionRepository(
                normalizedTransaction,
                normalizedTransactionLookupId),

            new FakeAuditLogWriter(),

            new FakeUnitOfWork(),

            new FakeAiProvider());
    }

    private static ReconciliationException CreateException()
    {
        return new ReconciliationException(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExceptionCategory.AmountMismatch,
            "Payment,Bank,Settlement",
            """{"paymentAmount":3500,"bankAmount":3490}""");
    }

    private static NormalizedTransaction
        CreateNormalizedTransaction(
            Guid runId)
    {
        return new NormalizedTransaction(
            runId,
            "TXN-0001",
            null,
            null,
            null);
    }

    private sealed class FakeAiProvider
        : IAiProvider
    {
        private readonly AiExplanationResponse?
            _response;

        private readonly Exception?
            _providerException;

        public FakeAiProvider(
            AiExplanationResponse? response = null,
            Exception? providerException = null)
        {
            _response = response;
            _providerException = providerException;
        }

        public string ProviderName =>
            "TestProvider";

        public bool IsAvailable =>
            true;

        public Task<AiExplanationResponse>
            GenerateExplanationAsync(
                AiExplanationRequest request,
                CancellationToken cancellationToken = default)
        {
            if (_providerException is not null)
            {
                throw _providerException;
            }

            return Task.FromResult(
                _response ??
                new AiExplanationResponse
                {
                    Provider =
                        ProviderName,

                    Explanation =
                        "Test explanation.",

                    SuggestedCategory =
                        "AmountMismatch",

                    GeneratedAtUtc =
                        DateTime.UtcNow
                });
        }
    }

    private sealed class
        FakeReconciliationExceptionRepository
        : IReconciliationExceptionRepository
    {
        private readonly ReconciliationException?
            _exception;

        public FakeReconciliationExceptionRepository(
            ReconciliationException? exception)
        {
            _exception = exception;
        }

        public Task<ReconciliationException?>
            GetByIdAsync(
                Guid exceptionId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _exception?.Id == exceptionId
                    ? _exception
                    : null);
        }

        public Task<IReadOnlyList<ReconciliationException>>
            GetByRunIdAsync(
                Guid runId,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReconciliationException>
                result =
                    _exception is not null &&
                    _exception.RunId == runId
                        ? new[] { _exception }
                        : Array.Empty<ReconciliationException>();

            return Task.FromResult(
                result);
        }

        public Task<(IReadOnlyList<ReconciliationException> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReconciliationException>
                result =
                    _exception is not null &&
                    _exception.RunId == runId
                        ? new[] { _exception }
                        : Array.Empty<ReconciliationException>();

            var totalCount = result.Count;

            var items =
                result
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

            return Task.FromResult(
                (items as IReadOnlyList<ReconciliationException>, totalCount));
        }

        public Task AddAsync(
            ReconciliationException exception,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationException> exceptions,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class
        FakeReconciliationResultRepository
        : IReconciliationResultRepository
    {
        private readonly ReconciliationResult?
            _result;

        private readonly Guid?
            _lookupId;

        public FakeReconciliationResultRepository(
            ReconciliationResult? result,
            Guid? lookupId)
        {
            _result = result;
            _lookupId = lookupId;
        }

        public Task<ReconciliationResult?>
            GetByIdAsync(
                Guid resultId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _result is not null &&
                _lookupId == resultId
                    ? _result
                    : null);
        }

        public Task<IReadOnlyList<ReconciliationResult>>
            GetByRunIdAsync(
                Guid runId,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReconciliationResult>
                result =
                    _result is not null &&
                    _result.RunId == runId
                        ? new[] { _result }
                        : Array.Empty<ReconciliationResult>();

            return Task.FromResult(
                result);
        }

        public Task<(IReadOnlyList<ReconciliationResult> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReconciliationResult>
                result =
                    _result is not null &&
                    _result.RunId == runId
                        ? new[] { _result }
                        : Array.Empty<ReconciliationResult>();

            var totalCount = result.Count;

            var items =
                result
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

            return Task.FromResult(
                (items as IReadOnlyList<ReconciliationResult>, totalCount));
        }

        public Task AddAsync(
            ReconciliationResult result,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationResult> results,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class
        FakeNormalizedTransactionRepository
        : INormalizedTransactionRepository
    {
        private readonly NormalizedTransaction?
            _transaction;

        private readonly Guid?
            _lookupId;

        public FakeNormalizedTransactionRepository(
            NormalizedTransaction? transaction,
            Guid? lookupId)
        {
            _transaction = transaction;
            _lookupId = lookupId;
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<NormalizedTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<NormalizedTransaction>>
            GetByRunIdAsync(
                Guid runId,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<NormalizedTransaction>
                result =
                    _transaction is not null &&
                    _transaction.RunId == runId
                        ? new[] { _transaction }
                        : Array.Empty<NormalizedTransaction>();

            return Task.FromResult(
                result);
        }

        public Task<NormalizedTransaction?>
            GetByIdAsync(
                Guid transactionId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _transaction is not null &&
                _lookupId == transactionId
                    ? _transaction
                    : null);
        }
    }

    private sealed class FakeAuditLogWriter
        : IAuditLogWriter
    {
        public List<AuditLog> Events { get; } =
            new();

        public Task AddAsync(
            AuditLog auditLog,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditLog);

            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<AuditLog> auditLogs,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(auditLogs);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork
        : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;

            return Task.FromResult(1);
        }
    }
}
