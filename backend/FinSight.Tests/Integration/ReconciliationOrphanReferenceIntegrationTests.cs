using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Proves, through the real production pipeline (raw CSV rows -> ingestion
/// -> ReconciliationOrchestrator grouping/normalization -> MatchClassifier
/// -> persisted ReconciliationResult/ReconciliationException), that a Bank
/// or Settlement record with no corresponding Payment record is no longer
/// silently dropped from reconciliation output. Prior to this fix,
/// ReconciliationOrchestrator.ExecuteAsync iterated only paymentGroups.Keys,
/// so such a reference never produced a NormalizedTransaction, never
/// contributed to totalUnits, and MatchClassifier's SOURCE_ABSENT_PAYMENT
/// branch was unreachable dead code. These tests exercise the union-of-keys
/// iteration end to end -- they do NOT hand-build a ReconciliationEvidence
/// object, unlike MatchClassifierAdditionalTests.MissingPayment_ReturnsMissingPayment.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ReconciliationOrphanReferenceIntegrationTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture =
            new PostgresIntegrationFixture();
    }

    [Test]
    public async Task ExecuteAsync_BankRecordWithNoPayment_ProducesSourceAbsentPaymentException()
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

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var exceptionRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        // TXN-2001 is a normal, fully-anchored control transaction so the
        // batch is not trivially a single-reference edge case. TXN-2002
        // exists only in the Bank source -- no Payment row, no Settlement
        // row anywhere in this batch.
        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-002001,TXN-2001,1000.00,INR,2026-08-01,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-002001,TXN-2001,1000.00,INR,2026-08-01,CLEARED
                BANK-002002,TXN-2002,500.00,INR,2026-08-02,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-002001,TXN-2001,1000.00,INR,2026-08-01,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Integration Test - Bank Orphan (No Payment)",

                    CreatedBy =
                        "integration-test",

                    PaymentFile =
                        paymentsStream,

                    BankFile =
                        bankStream,

                    SettlementFile =
                        settlementStream
                });

        var runResult =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId =
                        ingestionResult.BatchId
                });

        // The orphan reference must be counted -- not silently dropped.
        Assert.That(
            runResult.TotalReconciliationUnits,
            Is.EqualTo(2));

        // Bucket-total consistency invariant: every reconciliation unit
        // falls into exactly one of the five mutually exclusive buckets.
        Assert.That(
            runResult.MatchedCount +
            runResult.MismatchedCount +
            runResult.MissingCount +
            runResult.DuplicateCount +
            runResult.UnresolvedCount,
            Is.EqualTo(runResult.TotalReconciliationUnits));

        Assert.Multiple(() =>
        {
            Assert.That(
                runResult.MatchedCount,
                Is.EqualTo(1));

            Assert.That(
                runResult.MissingCount,
                Is.EqualTo(1));

            Assert.That(
                runResult.MismatchedCount,
                Is.EqualTo(0));

            Assert.That(
                runResult.DuplicateCount,
                Is.EqualTo(0));

            Assert.That(
                runResult.UnresolvedCount,
                Is.EqualTo(0));
        });

        var normalizedTransactions =
            await dbContext
                .Set<FinSight.Domain.Entities.NormalizedTransaction>()
                .AsNoTracking()
                .Where(x => x.RunId == runResult.RunId)
                .ToListAsync();

        var orphanTransaction =
            normalizedTransactions.SingleOrDefault(
                x => x.TransactionReference == "TXN-2002");

        Assert.That(
            orphanTransaction,
            Is.Not.Null,
            "TXN-2002 (Bank-only, no Payment) must produce a " +
            "NormalizedTransaction -- it must not disappear from the run.");

        Assert.That(
            orphanTransaction!.PaymentRecordId,
            Is.Null);

        Assert.That(
            orphanTransaction.BankRecordId,
            Is.Not.Null);

        var results =
            await resultRepository.GetByRunIdAsync(
                runResult.RunId);

        var orphanResult =
            results.Single(
                x => x.NormalizedTransactionId == orphanTransaction.Id);

        Assert.That(
            orphanResult.Status,
            Is.EqualTo(MatchStatus.Missing));

        Assert.That(
            orphanResult.ReasonCode,
            Is.EqualTo(ReconciliationReasonCode.SOURCE_ABSENT_PAYMENT));

        var exceptions =
            await exceptionRepository.GetByRunIdAsync(
                runResult.RunId);

        var orphanException =
            exceptions.Single(
                x => x.ReconciliationResultId == orphanResult.Id);

        Assert.That(
            orphanException.Category,
            Is.EqualTo(ExceptionCategory.MissingRecord));

        Assert.That(
            orphanException.InvolvedSources,
            Is.EqualTo("Bank"));
    }

    [Test]
    public async Task ExecuteAsync_SettlementRecordWithNoPayment_ProducesSourceAbsentPaymentException()
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

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var exceptionRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        // TXN-3001 is a normal, fully-anchored control transaction. TXN-3002
        // exists only in the Settlement source -- no Payment row, no Bank
        // row anywhere in this batch. Proves the union covers Settlement
        // orphans independently of the Bank orphan case above.
        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-003001,TXN-3001,2000.00,INR,2026-08-01,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-003001,TXN-3001,2000.00,INR,2026-08-01,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-003001,TXN-3001,2000.00,INR,2026-08-01,SETTLED
                SET-003002,TXN-3002,750.00,INR,2026-08-03,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Integration Test - Settlement Orphan (No Payment)",

                    CreatedBy =
                        "integration-test",

                    PaymentFile =
                        paymentsStream,

                    BankFile =
                        bankStream,

                    SettlementFile =
                        settlementStream
                });

        var runResult =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId =
                        ingestionResult.BatchId
                });

        Assert.That(
            runResult.TotalReconciliationUnits,
            Is.EqualTo(2));

        Assert.That(
            runResult.MatchedCount +
            runResult.MismatchedCount +
            runResult.MissingCount +
            runResult.DuplicateCount +
            runResult.UnresolvedCount,
            Is.EqualTo(runResult.TotalReconciliationUnits));

        Assert.Multiple(() =>
        {
            Assert.That(
                runResult.MatchedCount,
                Is.EqualTo(1));

            Assert.That(
                runResult.MissingCount,
                Is.EqualTo(1));
        });

        var normalizedTransactions =
            await dbContext
                .Set<FinSight.Domain.Entities.NormalizedTransaction>()
                .AsNoTracking()
                .Where(x => x.RunId == runResult.RunId)
                .ToListAsync();

        var orphanTransaction =
            normalizedTransactions.SingleOrDefault(
                x => x.TransactionReference == "TXN-3002");

        Assert.That(
            orphanTransaction,
            Is.Not.Null,
            "TXN-3002 (Settlement-only, no Payment) must produce a " +
            "NormalizedTransaction -- it must not disappear from the run.");

        Assert.That(
            orphanTransaction!.PaymentRecordId,
            Is.Null);

        Assert.That(
            orphanTransaction.SettlementRecordId,
            Is.Not.Null);

        var results =
            await resultRepository.GetByRunIdAsync(
                runResult.RunId);

        var orphanResult =
            results.Single(
                x => x.NormalizedTransactionId == orphanTransaction.Id);

        Assert.That(
            orphanResult.Status,
            Is.EqualTo(MatchStatus.Missing));

        Assert.That(
            orphanResult.ReasonCode,
            Is.EqualTo(ReconciliationReasonCode.SOURCE_ABSENT_PAYMENT));

        var exceptions =
            await exceptionRepository.GetByRunIdAsync(
                runResult.RunId);

        var orphanException =
            exceptions.Single(
                x => x.ReconciliationResultId == orphanResult.Id);

        Assert.That(
            orphanException.Category,
            Is.EqualTo(ExceptionCategory.MissingRecord));

        Assert.That(
            orphanException.InvolvedSources,
            Is.EqualTo("Settlement"));
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content));
    }
}
