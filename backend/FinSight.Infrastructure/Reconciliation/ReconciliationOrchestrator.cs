using System.Diagnostics;
using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Reconciliation;

public sealed class ReconciliationOrchestrator : IReconciliationService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IPaymentRecordRepository _paymentRepository;
    private readonly IBankRecordRepository _bankRepository;
    private readonly ISettlementRecordRepository _settlementRepository;

    private readonly INormalizedTransactionRepository
        _normalizedTransactionRepository;

    private readonly IReconciliationRunRepository _runRepository;
    private readonly IReconciliationResultRepository _resultRepository;

    private readonly IReconciliationExceptionRepository
        _exceptionRepository;

    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IUnitOfWork _unitOfWork;

    private readonly IExactReferenceMatchStrategy _strategyOne;

    private readonly IAmountDateToleranceMatchStrategy _strategyTwo;

    private readonly MatchClassifier _classifier;

    public ReconciliationOrchestrator(
        IBatchRepository batchRepository,
        IPaymentRecordRepository paymentRepository,
        IBankRecordRepository bankRepository,
        ISettlementRecordRepository settlementRepository,
        INormalizedTransactionRepository normalizedTransactionRepository,
        IReconciliationRunRepository runRepository,
        IReconciliationResultRepository resultRepository,
        IReconciliationExceptionRepository exceptionRepository,
        IAuditLogWriter auditLogWriter,
        IUnitOfWork unitOfWork,
        IExactReferenceMatchStrategy strategyOne,
        IAmountDateToleranceMatchStrategy strategyTwo,
        MatchClassifier classifier)
    {
        _batchRepository = batchRepository;
        _paymentRepository = paymentRepository;
        _bankRepository = bankRepository;
        _settlementRepository = settlementRepository;

        _normalizedTransactionRepository =
            normalizedTransactionRepository;

        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _exceptionRepository = exceptionRepository;

        _auditLogWriter = auditLogWriter;
        _unitOfWork = unitOfWork;

        _strategyOne = strategyOne;
        _strategyTwo = strategyTwo;
        _classifier = classifier;
    }

    public async Task<ReconciliationRunResult> ExecuteAsync(
        ReconciliationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.BatchId == Guid.Empty)
        {
            throw new ArgumentException(
                "Batch ID is required.",
                nameof(request));
        }

        // Phase 9: additive wall-clock timing only -- this measures the
        // reconciliation execution itself (batch lookup through the full
        // matching/classification loop and in-memory persistence staging).
        // It never alters, and is never used by, any matching, tolerance,
        // or classification decision. Started only once the request has
        // passed validation, since a validation failure isn't "execution."
        var stopwatch = Stopwatch.StartNew();

        var batch =
            await _batchRepository.GetByIdAsync(
                request.BatchId,
                cancellationToken);

        if (batch is null)
        {
            throw new KeyNotFoundException(
                $"Batch '{request.BatchId}' was not found.");
        }

        // Every execution receives a new run.
        var run =
            new ReconciliationRun(batch.Id);

        await _runRepository.AddAsync(
            run,
            cancellationToken);

        try
        {
            run.MarkRunning();

            var startedPayload =
                JsonSerializer.Serialize(
                    new
                    {
                        run_id = run.Id,
                        batch_id = batch.Id,
                        status = run.Status.ToString()
                    });

            await _auditLogWriter.AddAsync(
                new AuditLog(
                    AuditEventType.ReconciliationStarted,
                    startedPayload,
                    run.Id,
                    relatedEntityType:
                        "ReconciliationRun",
                    relatedEntityId:
                        run.Id),
                cancellationToken);

            var payments =
                await _paymentRepository.GetByBatchIdAsync(
                    batch.Id,
                    cancellationToken);

            var banks =
                await _bankRepository.GetByBatchIdAsync(
                    batch.Id,
                    cancellationToken);

            var settlements =
                await _settlementRepository.GetByBatchIdAsync(
                    batch.Id,
                    cancellationToken);

            // Payment is the primary reconciliation anchor.
            var paymentGroups =
                payments
                    .GroupBy(
                        x => x.TransactionReference)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                            (IReadOnlyList<PaymentRecord>)
                            group.ToList(),
                        StringComparer.Ordinal);

            // Supporting lookup dictionaries.
            var bankGroups =
                banks
                    .GroupBy(
                        x => x.TransactionReference)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                            (IReadOnlyList<BankRecord>)
                            group.ToList(),
                        StringComparer.Ordinal);

            var settlementGroups =
                settlements
                    .GroupBy(
                        x => x.TransactionReference)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                            (IReadOnlyList<SettlementRecord>)
                            group.ToList(),
                        StringComparer.Ordinal);

            var normalizedTransactions =
                new List<NormalizedTransaction>();

            var reconciliationResults =
                new List<ReconciliationResult>();

            var reconciliationExceptions =
                new List<ReconciliationException>();

            // Every reference present in ANY source is a reconciliation
            // unit — not just Payment-anchored references. A Bank or
            // Settlement record with no matching Payment must still be
            // evaluated (and classified as SOURCE_ABSENT_PAYMENT), never
            // silently dropped from the run.
            var allReferences =
                paymentGroups.Keys
                    .Union(
                        bankGroups.Keys,
                        StringComparer.Ordinal)
                    .Union(
                        settlementGroups.Keys,
                        StringComparer.Ordinal);

            foreach (
                var transactionReference
                in allReferences.OrderBy(
                    x => x,
                    StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                paymentGroups.TryGetValue(
                    transactionReference,
                    out var paymentRecords);

                paymentRecords ??=
                    Array.Empty<PaymentRecord>();

                bankGroups.TryGetValue(
                    transactionReference,
                    out var bankRecords);

                settlementGroups.TryGetValue(
                    transactionReference,
                    out var settlementRecords);

                bankRecords ??=
                    Array.Empty<BankRecord>();

                settlementRecords ??=
                    Array.Empty<SettlementRecord>();

                var evidence =
                    new ReconciliationEvidence
                    {
                        TransactionReference =
                            transactionReference,

                        Payments =
                            paymentRecords,

                        Banks =
                            bankRecords,

                        Settlements =
                            settlementRecords
                    };

                // Strategy 1 produces exact-comparison evidence.
                var exactEvidence =
                    _strategyOne.Evaluate(
                        evidence);

                // Strategy 2 consumes Strategy 1 evidence.
                var toleranceEvidence =
                    _strategyTwo.Evaluate(
                        evidence,
                        exactEvidence);

                // MatchClassifier owns the final decision.
                var decision =
                    _classifier.Classify(
                        evidence,
                        exactEvidence,
                        toleranceEvidence);

                var paymentId =
                    paymentRecords.Count == 1
                        ? paymentRecords[0].Id
                        : (Guid?)null;

                var bankId =
                    bankRecords.Count == 1
                        ? bankRecords[0].Id
                        : (Guid?)null;

                var settlementId =
                    settlementRecords.Count == 1
                        ? settlementRecords[0].Id
                        : (Guid?)null;

                var normalizedTransaction =
                    new NormalizedTransaction(
                        run.Id,
                        transactionReference,
                        paymentId,
                        bankId,
                        settlementId);

                normalizedTransactions.Add(
                    normalizedTransaction);

                var reconciliationResult =
                    new ReconciliationResult(
                        run.Id,
                        normalizedTransaction.Id,
                        decision.Status,
                        decision.ReasonCode,
                        decision.StrategyUsed);

                reconciliationResults.Add(
                    reconciliationResult);

                var decisionPayload =
                    JsonSerializer.Serialize(
                        new
                        {
                            run_id = run.Id,

                            transaction_reference =
                                transactionReference,

                            result_id =
                                reconciliationResult.Id,

                            status =
                                decision.Status.ToString(),

                            reason_code =
                                decision.ReasonCode.ToString(),

                            strategy_used =
                                decision.StrategyUsed
                        });

                await _auditLogWriter.AddAsync(
                    new AuditLog(
                        AuditEventType.ReconciliationDecisionRecorded,
                        decisionPayload,
                        run.Id,
                        relatedEntityType:
                            "ReconciliationResult",
                        relatedEntityId:
                            reconciliationResult.Id),
                    cancellationToken);

                // Exactly one exception for every non-Matched unit.
                if (decision.Status != MatchStatus.Matched)
                {
                    var exceptionDetail =
                        BuildExceptionDetail(
                            transactionReference,
                            evidence,
                            exactEvidence,
                            toleranceEvidence);

                    var involvedSources =
                        BuildInvolvedSources(
                            evidence);

                    var exception =
                        new ReconciliationException(
                            run.Id,
                            reconciliationResult.Id,
                            decision.ExceptionCategory
                                ?? ExceptionCategory.Unresolved,
                            involvedSources,
                            exceptionDetail);

                    reconciliationExceptions.Add(
                        exception);

                    var exceptionPayload =
                        JsonSerializer.Serialize(
                            new
                            {
                                run_id = run.Id,

                                transaction_reference =
                                    transactionReference,

                                exception_id =
                                    exception.Id,

                                result_id =
                                    reconciliationResult.Id,

                                category =
                                    (
                                        decision.ExceptionCategory
                                        ?? ExceptionCategory.Unresolved
                                    ).ToString()
                            });

                    await _auditLogWriter.AddAsync(
                        new AuditLog(
                            AuditEventType.ExceptionCreated,
                            exceptionPayload,
                            run.Id,
                            relatedEntityType:
                                "ReconciliationException",
                            relatedEntityId:
                                exception.Id),
                        cancellationToken);
                }
            }

            var totalUnits =
                normalizedTransactions.Count;

            var matchedCount =
                reconciliationResults.Count(
                    x =>
                        x.Status ==
                        MatchStatus.Matched);

            var mismatchedCount =
                reconciliationResults.Count(
                    x =>
                        x.Status ==
                        MatchStatus.Mismatched);

            var missingCount =
                reconciliationResults.Count(
                    x =>
                        x.Status ==
                        MatchStatus.Missing);

            var duplicateCount =
                reconciliationResults.Count(
                    x =>
                        x.Status ==
                        MatchStatus.Duplicate);

            var unresolvedCount =
                reconciliationResults.Count(
                    x =>
                        x.Status ==
                        MatchStatus.Unresolved);

            // Match rate denominator is logical reconciliation units,
            // never raw CSV row counts.
            var matchRate =
                totalUnits == 0
                    ? 0.00m
                    : decimal.Round(
                        ((decimal)matchedCount /
                         totalUnits) *
                        100m,
                        2);

            run.Complete(
                totalUnits,
                matchRate);

            await _normalizedTransactionRepository.AddRangeAsync(
                normalizedTransactions,
                cancellationToken);

            await _resultRepository.AddRangeAsync(
                reconciliationResults,
                cancellationToken);

            if (reconciliationExceptions.Count > 0)
            {
                await _exceptionRepository.AddRangeAsync(
                    reconciliationExceptions,
                    cancellationToken);
            }

            stopwatch.Stop();

            var elapsedSeconds =
                stopwatch.Elapsed.TotalSeconds;

            // Guarded against a zero-duration edge case: dividing by a
            // positive elapsed time is the exact totalUnits/duration.TotalSeconds
            // formula; a defensive zero-duration fallback avoids ever
            // serializing a non-finite (Infinity/NaN) value, which
            // System.Text.Json cannot represent as JSON.
            var recordsPerSecond =
                elapsedSeconds > 0
                    ? totalUnits / elapsedSeconds
                    : 0d;

            var completedPayload =
                JsonSerializer.Serialize(
                    new
                    {
                        run_id = run.Id,
                        batch_id = batch.Id,
                        status = run.Status.ToString(),
                        total_units = totalUnits,
                        matched = matchedCount,
                        mismatched = mismatchedCount,
                        missing = missingCount,
                        duplicate = duplicateCount,
                        unresolved = unresolvedCount,
                        match_rate = matchRate,
                        duration_ms = stopwatch.ElapsedMilliseconds,
                        records_per_second = recordsPerSecond
                    });

            await _auditLogWriter.AddAsync(
                new AuditLog(
                    AuditEventType.ReconciliationCompleted,
                    completedPayload,
                    run.Id,
                    relatedEntityType:
                        "ReconciliationRun",
                    relatedEntityId:
                        run.Id),
                cancellationToken);

            // Synchronous persistence.
            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return new ReconciliationRunResult
            {
                RunId =
                    run.Id,

                BatchId =
                    batch.Id,

                Status =
                    run.Status,

                TotalReconciliationUnits =
                    totalUnits,

                MatchedCount =
                    matchedCount,

                MismatchedCount =
                    mismatchedCount,

                MissingCount =
                    missingCount,

                DuplicateCount =
                    duplicateCount,

                UnresolvedCount =
                    unresolvedCount,

                MatchRate =
                    matchRate
            };
        }
        catch (Exception ex)
        {
            run.Fail();

            var failedPayload =
                JsonSerializer.Serialize(
                    new
                    {
                        run_id = run.Id,
                        batch_id = batch.Id,
                        status = run.Status.ToString(),
                        error_type =
                            ex.GetType().Name,
                        error_message =
                            ex.Message
                    });

            await _auditLogWriter.AddAsync(
                new AuditLog(
                    AuditEventType.ReconciliationFailed,
                    failedPayload,
                    run.Id,
                    relatedEntityType:
                        "ReconciliationRun",
                    relatedEntityId:
                        run.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            throw;
        }
    }

    private static string BuildInvolvedSources(
        ReconciliationEvidence evidence)
    {
        var sources =
            new List<string>();

        if (evidence.HasPayment)
        {
            sources.Add("Payment");
        }

        if (evidence.HasBank)
        {
            sources.Add("Bank");
        }

        if (evidence.HasSettlement)
        {
            sources.Add("Settlement");
        }

        return string.Join(
            ",",
            sources);
    }

    private static string BuildExceptionDetail(
        string transactionReference,
        ReconciliationEvidence evidence,
        StrategyEvidence exactEvidence,
        StrategyEvidence toleranceEvidence)
    {
        var detail =
            new
            {
                transaction_reference =
                    transactionReference,

                payment =
                    evidence.Payments.Select(
                        x =>
                            new
                            {
                                source_record_identifier =
                                    x.SourceRecordIdentifier,

                                amount =
                                    x.Amount,

                                currency =
                                    x.Currency,

                                transaction_date =
                                    x.TransactionDate,

                                status =
                                    x.Status
                            }),

                bank =
                    evidence.Banks.Select(
                        x =>
                            new
                            {
                                source_record_identifier =
                                    x.SourceRecordIdentifier,

                                amount =
                                    x.Amount,

                                currency =
                                    x.Currency,

                                transaction_date =
                                    x.TransactionDate,

                                status =
                                    x.Status
                            }),

                settlement =
                    evidence.Settlements.Select(
                        x =>
                            new
                            {
                                source_record_identifier =
                                    x.SourceRecordIdentifier,

                                amount =
                                    x.Amount,

                                currency =
                                    x.Currency,

                                transaction_date =
                                    x.TransactionDate,

                                status =
                                    x.Status
                            }),

                exact_evidence =
                    new
                    {
                        sources_present =
                            exactEvidence.SourcesPresent,

                        exact_reference_match =
                            exactEvidence.ExactReferenceMatch,

                        exact_amount_match =
                            exactEvidence.ExactAmountMatch,

                        exact_date_match =
                            exactEvidence.ExactDateMatch,

                        non_comparable_business_state =
                            exactEvidence.NonComparableBusinessState,

                        non_comparable_reason =
                            exactEvidence.NonComparableReason
                    },

                tolerance_evidence =
                    new
                    {
                        amount_within_tolerance =
                            toleranceEvidence.AmountWithinTolerance,

                        date_within_tolerance =
                            toleranceEvidence.DateWithinTolerance,

                        amount_mismatch =
                            toleranceEvidence.AmountMismatch,

                        date_mismatch =
                            toleranceEvidence.DateMismatch
                    }
            };

        return JsonSerializer.Serialize(
            detail,
            new JsonSerializerOptions
            {
                WriteIndented = false
            });
    }
}