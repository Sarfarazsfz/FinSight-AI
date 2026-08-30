using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class ReconciliationPipelineIntegrationTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture =
            new PostgresIntegrationFixture();
    }

    [Test]
    public async Task MixedBatch_Ingests_Reconciles_AndPersistsExpectedResults()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var ingestionService =
            scope.ServiceProvider
                .GetRequiredService<IBatchIngestionService>();

        var reconciliationService =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationService>();

        var batchRepository =
            scope.ServiceProvider
                .GetRequiredService<IBatchRepository>();

        var runRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationRunRepository>();

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var exceptionRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-000001,TXN-0001,1000.00,INR,2026-08-01,COMPLETED
                PAY-000002,TXN-0002,2000.00,INR,2026-08-02,COMPLETED
                PAY-000003,TXN-0003,3000.00,INR,2026-08-03,COMPLETED
                PAY-000004,TXN-0004,4000.00,INR,2026-08-04,COMPLETED
                PAY-000005,TXN-0005,5000.00,INR,2026-08-05,COMPLETED
                PAY-000006,TXN-0006,6000.00,INR,2026-08-06,COMPLETED
                PAY-000007,TXN-0007,7000.00,INR,2026-08-07,COMPLETED
                PAY-000008,TXN-0007,7000.00,INR,2026-08-07,COMPLETED
                PAY-000009,TXN-0008,8000.00,INR,2026-08-08,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-000001,TXN-0001,1000.00,INR,2026-08-01,CLEARED
                BANK-000002,TXN-0002,2000.00,INR,2026-08-03,CLEARED
                BANK-000003,TXN-0003,2990.00,INR,2026-08-03,CLEARED
                BANK-000004,TXN-0004,4000.00,INR,2026-08-07,CLEARED
                BANK-000006,TXN-0006,6000.00,INR,2026-08-06,CLEARED
                BANK-000007,TXN-0007,7000.00,INR,2026-08-07,CLEARED
                BANK-000008,TXN-0008,8000.00,INR,2026-08-08,REVERSED_FRAUD
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-000001,TXN-0001,1000.00,INR,2026-08-01,SETTLED
                SET-000002,TXN-0002,2000.00,INR,2026-08-03,SETTLED
                SET-000003,TXN-0003,2990.00,INR,2026-08-03,SETTLED
                SET-000004,TXN-0004,4000.00,INR,2026-08-07,SETTLED
                SET-000005,TXN-0005,5000.00,INR,2026-08-05,SETTLED
                SET-000007,TXN-0007,7000.00,INR,2026-08-07,SETTLED
                SET-000008,TXN-0008,8000.00,INR,2026-08-08,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Integration Test - Mixed Reconciliation",

                    CreatedBy =
                        "integration-test",

                    PaymentFile =
                        paymentsStream,

                    BankFile =
                        bankStream,

                    SettlementFile =
                        settlementStream
                });

        Assert.That(
            ingestionResult.ValidationStatus,
            Is.EqualTo("Valid"));

        Assert.That(
            ingestionResult.PaymentRecordCount,
            Is.EqualTo(9));

        Assert.That(
            ingestionResult.BankRecordCount,
            Is.EqualTo(7));

        Assert.That(
            ingestionResult.SettlementRecordCount,
            Is.EqualTo(7));

        Assert.That(
            ingestionResult.TotalRecordCount,
            Is.EqualTo(23));

        var batch =
            await batchRepository.GetByIdAsync(
                ingestionResult.BatchId);

        Assert.That(batch, Is.Not.Null);

        var runResult =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId =
                        ingestionResult.BatchId
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                runResult.Status,
                Is.EqualTo(
                    ReconciliationRunStatus.Completed));

            Assert.That(
                runResult.TotalReconciliationUnits,
                Is.EqualTo(8));

            Assert.That(
                runResult.MatchedCount,
                Is.EqualTo(2));

            Assert.That(
                runResult.MismatchedCount,
                Is.EqualTo(2));

            Assert.That(
                runResult.MissingCount,
                Is.EqualTo(2));

            Assert.That(
                runResult.DuplicateCount,
                Is.EqualTo(1));

            Assert.That(
                runResult.UnresolvedCount,
                Is.EqualTo(1));

            Assert.That(
                runResult.MatchRate,
                Is.EqualTo(25.00m));
        });

        var run =
            await runRepository.GetByIdAsync(
                runResult.RunId);

        Assert.That(run, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                run!.TotalReconciliationUnits,
                Is.EqualTo(8));

            Assert.That(
                run.MatchRate,
                Is.EqualTo(25.00m));

            Assert.That(
                run.Status,
                Is.EqualTo(
                    ReconciliationRunStatus.Completed));

            Assert.That(
                run.CompletedAt,
                Is.Not.Null);
        });

        var results =
            await resultRepository.GetByRunIdAsync(
                runResult.RunId);

        Assert.That(
            results,
            Has.Count.EqualTo(8));

        var normalizedTransactions =
            await dbContext
                .Set<FinSight.Domain.Entities.NormalizedTransaction>()
                .AsNoTracking()
                .Where(
                    x => x.RunId == runResult.RunId)
                .ToListAsync();

        Assert.That(
            normalizedTransactions,
            Has.Count.EqualTo(8));

        Assert.That(
            normalizedTransactions
                .Select(x => x.TransactionReference)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            Is.EqualTo(8));

        Assert.That(
            results.Count(
                x =>
                    x.Status ==
                    MatchStatus.Matched),
            Is.EqualTo(2));

        Assert.That(
            results.Count(
                x =>
                    x.Status ==
                    MatchStatus.Mismatched),
            Is.EqualTo(2));

        Assert.That(
            results.Count(
                x =>
                    x.Status ==
                    MatchStatus.Missing),
            Is.EqualTo(2));

        Assert.That(
            results.Count(
                x =>
                    x.Status ==
                    MatchStatus.Duplicate),
            Is.EqualTo(1));

        Assert.That(
            results.Count(
                x =>
                    x.Status ==
                    MatchStatus.Unresolved),
            Is.EqualTo(1));

        var exceptions =
            await exceptionRepository.GetByRunIdAsync(
                runResult.RunId);

        Assert.That(
            exceptions,
            Has.Count.EqualTo(6));

        var expectedReferencesWithExceptions =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "TXN-0003",
                "TXN-0004",
                "TXN-0005",
                "TXN-0006",
                "TXN-0007",
                "TXN-0008"
            };

        var normalizedById =
            normalizedTransactions.ToDictionary(
                x => x.Id);

        var resultReferences =
            results.ToDictionary(
                x => x.Id,
                x =>
                    normalizedById[
                        x.NormalizedTransactionId]
                    .TransactionReference);

        var exceptionReferences =
            exceptions
                .Select(
                    x => resultReferences[
                        x.ReconciliationResultId])
                .ToHashSet(
                    StringComparer.Ordinal);

        Assert.That(
            exceptionReferences,
            Is.EqualTo(
                expectedReferencesWithExceptions));

        Assert.That(
            results
                .Where(
                    x =>
                        x.Status ==
                        MatchStatus.Matched)
                .Select(x => x.Id)
                .Intersect(
                    exceptions.Select(
                        x => x.ReconciliationResultId))
                .Count(),
            Is.EqualTo(0));

        // ------------------------------------------------------------
        // AUDIT LOG VERIFICATION
        // ------------------------------------------------------------

        var auditLogs =
            await dbContext.AuditLogs
                .AsNoTracking()
                .OrderBy(x => x.OccurredAt)
                .ToListAsync();

        Assert.That(
            auditLogs,
            Has.Count.EqualTo(18));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.BatchCreated),
            Is.EqualTo(1));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.BatchValidated),
            Is.EqualTo(1));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.ReconciliationStarted),
            Is.EqualTo(1));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.ReconciliationDecisionRecorded),
            Is.EqualTo(8));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.ExceptionCreated),
            Is.EqualTo(6));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.ReconciliationCompleted),
            Is.EqualTo(1));

        Assert.That(
            auditLogs.Count(
                x =>
                    x.EventType ==
                    AuditEventType.ReconciliationFailed),
            Is.EqualTo(0));

        var batchAuditLogs =
            auditLogs
                .Where(
                    x =>
                        x.EventType ==
                            AuditEventType.BatchCreated ||
                        x.EventType ==
                            AuditEventType.BatchValidated)
                .ToList();

        Assert.That(
            batchAuditLogs,
            Has.Count.EqualTo(2));

        Assert.That(
            batchAuditLogs.All(
                x =>
                    x.RunId == null &&
                    x.RelatedEntityType == "Batch" &&
                    x.RelatedEntityId ==
                        ingestionResult.BatchId &&
                    !string.IsNullOrWhiteSpace(
                        x.DetailPayload)),
            Is.True);

        var runAuditLogs =
            auditLogs
                .Where(
                    x =>
                        x.EventType ==
                            AuditEventType.ReconciliationStarted ||
                        x.EventType ==
                            AuditEventType.ReconciliationCompleted)
                .ToList();

        Assert.That(
            runAuditLogs,
            Has.Count.EqualTo(2));

        Assert.That(
            runAuditLogs.All(
                x =>
                    x.RunId == runResult.RunId &&
                    x.RelatedEntityType ==
                        "ReconciliationRun" &&
                    x.RelatedEntityId ==
                        runResult.RunId &&
                    !string.IsNullOrWhiteSpace(
                        x.DetailPayload)),
            Is.True);

        var decisionAuditLogs =
            auditLogs
                .Where(
                    x =>
                        x.EventType ==
                        AuditEventType.ReconciliationDecisionRecorded)
                .ToList();

        Assert.That(
            decisionAuditLogs,
            Has.Count.EqualTo(8));

        Assert.That(
            decisionAuditLogs.All(
                x =>
                    x.RunId == runResult.RunId &&
                    x.RelatedEntityType ==
                        "ReconciliationResult" &&
                    x.RelatedEntityId.HasValue &&
                    results.Any(
                        result =>
                            result.Id ==
                            x.RelatedEntityId.Value) &&
                    !string.IsNullOrWhiteSpace(
                        x.DetailPayload)),
            Is.True);

        var exceptionAuditLogs =
            auditLogs
                .Where(
                    x =>
                        x.EventType ==
                        AuditEventType.ExceptionCreated)
                .ToList();

        Assert.That(
            exceptionAuditLogs,
            Has.Count.EqualTo(6));

        Assert.That(
            exceptionAuditLogs.All(
                x =>
                    x.RunId == runResult.RunId &&
                    x.RelatedEntityType ==
                        "ReconciliationException" &&
                    x.RelatedEntityId.HasValue &&
                    exceptions.Any(
                        exception =>
                            exception.Id ==
                            x.RelatedEntityId.Value) &&
                    !string.IsNullOrWhiteSpace(
                        x.DetailPayload)),
            Is.True);

        Assert.That(
            auditLogs.All(
                x =>
                    !string.IsNullOrWhiteSpace(
                        x.DetailPayload)),
            Is.True);
    }

    [Test]
    public async Task InvalidBatch_IsRejected_AndPersistsNothing()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var ingestionService =
            scope.ServiceProvider
                .GetRequiredService<IBatchIngestionService>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                INVALID-ID,TXN-INVALID,1000.00,INR,2026-08-01,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-INT-INVALID,TXN-INVALID,1000.00,INR,2026-08-01,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-INT-INVALID,TXN-INVALID,1000.00,INR,2026-08-01,SETTLED
                """);

        Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await ingestionService.IngestAsync(
                    new BatchIngestionRequest
                    {
                        BatchLabel =
                            "Integration Test - Invalid Batch",

                        CreatedBy =
                            "integration-test",

                        PaymentFile =
                            paymentsStream,

                        BankFile =
                            bankStream,

                        SettlementFile =
                            settlementStream
                    }));

        Assert.That(
            await dbContext.Batches.CountAsync(),
            Is.EqualTo(0));

        Assert.That(
            await dbContext.PaymentRecords.CountAsync(),
            Is.EqualTo(0));

        Assert.That(
            await dbContext.BankRecords.CountAsync(),
            Is.EqualTo(0));

        Assert.That(
            await dbContext.SettlementRecords.CountAsync(),
            Is.EqualTo(0));

        Assert.That(
            await dbContext.AuditLogs.CountAsync(),
            Is.EqualTo(0));
    }

    [Test]
    public async Task MissingBatch_ReconciliationFailsWithoutCreatingRun()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var reconciliationService =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationService>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var missingBatchId =
            Guid.NewGuid();

        var exception =
            Assert.ThrowsAsync<KeyNotFoundException>(
                async () =>
                    await reconciliationService.ExecuteAsync(
                        new ReconciliationRunRequest
                        {
                            BatchId =
                                missingBatchId
                        }));

        Assert.That(
            exception!.Message,
            Does.Contain(
                missingBatchId.ToString()));

        Assert.That(
            await dbContext.ReconciliationRuns.CountAsync(),
            Is.EqualTo(0));

        Assert.That(
            await dbContext.AuditLogs.CountAsync(),
            Is.EqualTo(0));
    }

    /// <summary>
    /// Phase 10 (Flexible CSV Column Mapping) requirement: reconciliation
    /// behavior must be unchanged after flexible header mapping. Rather
    /// than hand-predict expected match/mismatch/missing counts for a new
    /// dataset (which would risk assuming undocumented classification
    /// rules), this ingests and reconciles the exact same row data twice
    /// -- once with canonical headers, once with approved-alias headers
    /// in a different column order and case -- and asserts the two runs
    /// produce identical aggregate counts, match rate, and per-reference
    /// Status assignments. This proves header aliasing is invisible to
    /// reconciliation, without this test needing to know what "correct"
    /// classification looks like.
    /// </summary>
    [Test]
    public async Task AliasHeaderBatch_ReconciliationOutcome_IsIdenticalToCanonicalHeaderBatch()
    {
        await _fixture.ResetDatabaseAsync();

        await using (var canonicalScope = _fixture.CreateScope())
        {
            var canonicalOutcome =
                await IngestAndReconcileAsync(
                    canonicalScope,
                    paymentsCsv:
                        """
                        payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                        PAY-100001,TXN-0001,1000.00,INR,2026-08-01,COMPLETED
                        PAY-100002,TXN-0002,2000.00,INR,2026-08-02,COMPLETED
                        PAY-100003,TXN-0003,3000.00,INR,2026-08-03,COMPLETED
                        """,
                    bankCsv:
                        """
                        bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                        BANK-100001,TXN-0001,1000.00,INR,2026-08-01,CLEARED
                        BANK-100002,TXN-0002,2500.00,INR,2026-08-02,CLEARED
                        """,
                    settlementCsv:
                        """
                        settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                        SET-100001,TXN-0001,1000.00,INR,2026-08-01,SETTLED
                        SET-100002,TXN-0002,2500.00,INR,2026-08-02,SETTLED
                        """);

            await _fixture.ResetDatabaseAsync();

            await using var aliasScope = _fixture.CreateScope();

            var aliasOutcome =
                await IngestAndReconcileAsync(
                    aliasScope,
                    paymentsCsv:
                        """
                        payment_state,txn_date,currency_code,amount_paid,payment_id,txn_ref
                        COMPLETED,2026-08-01,INR,1000.00,PAY-100001,TXN-0001
                        COMPLETED,2026-08-02,INR,2000.00,PAY-100002,TXN-0002
                        COMPLETED,2026-08-03,INR,3000.00,PAY-100003,TXN-0003
                        """,
                    bankCsv:
                        """
                        Bank_State,Txn_Date,Currency_Code,Amount_Paid,Bank_Id,Txn_Ref
                        CLEARED,2026-08-01,INR,1000.00,BANK-100001,TXN-0001
                        CLEARED,2026-08-02,INR,2500.00,BANK-100002,TXN-0002
                        """,
                    settlementCsv:
                        """
                        settlement-state,txn-date,currency-code,amount-paid,settlement-id,txn-ref
                        SETTLED,2026-08-01,INR,1000.00,SET-100001,TXN-0001
                        SETTLED,2026-08-02,INR,2500.00,SET-100002,TXN-0002
                        """);

            Assert.Multiple(() =>
            {
                Assert.That(
                    aliasOutcome.IngestionResult.PaymentRecordCount,
                    Is.EqualTo(canonicalOutcome.IngestionResult.PaymentRecordCount));

                Assert.That(
                    aliasOutcome.IngestionResult.BankRecordCount,
                    Is.EqualTo(canonicalOutcome.IngestionResult.BankRecordCount));

                Assert.That(
                    aliasOutcome.IngestionResult.SettlementRecordCount,
                    Is.EqualTo(canonicalOutcome.IngestionResult.SettlementRecordCount));

                Assert.That(
                    aliasOutcome.RunResult.TotalReconciliationUnits,
                    Is.EqualTo(canonicalOutcome.RunResult.TotalReconciliationUnits));

                Assert.That(
                    aliasOutcome.RunResult.MatchedCount,
                    Is.EqualTo(canonicalOutcome.RunResult.MatchedCount));

                Assert.That(
                    aliasOutcome.RunResult.MismatchedCount,
                    Is.EqualTo(canonicalOutcome.RunResult.MismatchedCount));

                Assert.That(
                    aliasOutcome.RunResult.MissingCount,
                    Is.EqualTo(canonicalOutcome.RunResult.MissingCount));

                Assert.That(
                    aliasOutcome.RunResult.DuplicateCount,
                    Is.EqualTo(canonicalOutcome.RunResult.DuplicateCount));

                Assert.That(
                    aliasOutcome.RunResult.UnresolvedCount,
                    Is.EqualTo(canonicalOutcome.RunResult.UnresolvedCount));

                Assert.That(
                    aliasOutcome.RunResult.MatchRate,
                    Is.EqualTo(canonicalOutcome.RunResult.MatchRate));

                // Compare Status keyed by reference (not row order) --
                // both runs use the same TXN-000x references, which is
                // fine since normalized transactions are scoped per run.
                Assert.That(
                    aliasOutcome.StatusByReference.Keys,
                    Is.EquivalentTo(canonicalOutcome.StatusByReference.Keys));

                foreach (var reference in canonicalOutcome.StatusByReference.Keys)
                {
                    Assert.That(
                        aliasOutcome.StatusByReference[reference],
                        Is.EqualTo(canonicalOutcome.StatusByReference[reference]),
                        $"Status for {reference} differed between the canonical " +
                        "and alias-header runs.");
                }
            });
        }
    }

    private sealed record IngestAndReconcileOutcome(
        BatchIngestionResult IngestionResult,
        ReconciliationRunResult RunResult,
        Dictionary<string, MatchStatus> StatusByReference);

    private static async Task<IngestAndReconcileOutcome> IngestAndReconcileAsync(
        AsyncServiceScope scope,
        string paymentsCsv,
        string bankCsv,
        string settlementCsv)
    {
        var ingestionService =
            scope.ServiceProvider
                .GetRequiredService<IBatchIngestionService>();

        var reconciliationService =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationService>();

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        await using var paymentsStream = CreateStream(paymentsCsv);
        await using var bankStream = CreateStream(bankCsv);
        await using var settlementStream = CreateStream(settlementCsv);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel = "Phase 10 - Alias Equivalence Test",
                    CreatedBy = "integration-test",
                    PaymentFile = paymentsStream,
                    BankFile = bankStream,
                    SettlementFile = settlementStream
                });

        var runResult =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId = ingestionResult.BatchId
                });

        var results =
            await resultRepository.GetByRunIdAsync(runResult.RunId);

        var normalizedTransactions =
            await dbContext
                .Set<FinSight.Domain.Entities.NormalizedTransaction>()
                .AsNoTracking()
                .Where(x => x.RunId == runResult.RunId)
                .ToListAsync();

        var referenceById =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var statusByReference =
            results.ToDictionary(
                x => referenceById[x.NormalizedTransactionId],
                x => x.Status);

        return new IngestAndReconcileOutcome(
            ingestionResult,
            runResult,
            statusByReference);
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content));
    }
}