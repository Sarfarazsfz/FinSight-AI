using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Application.Evaluation;
using FinSight.DataGenerator.Generation;
using FinSight.DataGenerator.Models;
using FinSight.DataGenerator.Validation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Proves, through the real production pipeline, that the evaluation
/// layer this phase hardened actually works end to end:
///   1. The full deterministic 100-unit demo batch, generated fresh by
///      the real TransactionGenerator/SourceRowGenerator/
///      GroundTruthGenerator, is ingested and reconciled for real.
///   2. The real persisted ReconciliationResult/ReconciliationException
///      rows are compared, via GroundTruthComparator.Compare(), against
///      the SAME batch's independently generated ground truth.
///   3. Reconciliation is deterministic across repeated runs of the
///      same batch.
///   4. The stored ReconciliationRun.MatchRate agrees with an
///      independently recomputed rate.
///
/// Prior to Phase 2, none of this had ever been exercised: the
/// comparator's only real invocation path (GroundTruthComparator.
/// CompareAsync over HTTP) could not complete a run (wrong response
/// shape, no pagination, no auth), and FinSight.Tests had no project
/// reference to FinSight.DataGenerator at all.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class GroundTruthEndToEndIntegrationTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture =
            new PostgresIntegrationFixture();
    }

    [Test]
    public async Task FullGeneratedBatch_ReconciliationOutputMatchesIndependentGroundTruth()
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

        var runRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationRunRepository>();

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var exceptionRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var normalizedTransactionRepository =
            scope.ServiceProvider
                .GetRequiredService<INormalizedTransactionRepository>();

        // --------------------------------------------------------
        // Generate the SAME deterministic 100-unit batch the real
        // FinSight.DataGenerator console tool produces -- the actual
        // production classes, not a hand-rolled fixture.
        // --------------------------------------------------------
        var plannedTransactions =
            new TransactionGenerator().Generate();

        var sourceRows =
            new SourceRowGenerator().Generate(plannedTransactions);

        var groundTruthRows =
            new GroundTruthGenerator().Generate(plannedTransactions);

        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "finsight-ground-truth-e2e-" + Guid.NewGuid());

        try
        {
            new CsvWriter().WriteAll(
                sourceRows,
                groundTruthRows,
                tempDirectory);

            BatchIngestionResult ingestionResult;

            await using (var paymentsStream =
                             OpenRead(tempDirectory, "payments.csv"))
            await using (var bankStream =
                             OpenRead(tempDirectory, "bank.csv"))
            await using (var settlementStream =
                             OpenRead(tempDirectory, "settlements.csv"))
            {
                ingestionResult =
                    await ingestionService.IngestAsync(
                        new BatchIngestionRequest
                        {
                            BatchLabel =
                                "Ground-Truth End-to-End Integration Test",

                            CreatedBy =
                                "integration-test",

                            PaymentFile =
                                paymentsStream,

                            BankFile =
                                bankStream,

                            SettlementFile =
                                settlementStream
                        });
            }

            var runResult =
                await reconciliationService.ExecuteAsync(
                    new ReconciliationRunRequest
                    {
                        BatchId =
                            ingestionResult.BatchId
                    });

            // --------------------------------------------------------
            // 100-unit aggregate consistency (approved bucket design).
            // --------------------------------------------------------
            Assert.Multiple(() =>
            {
                Assert.That(
                    runResult.TotalReconciliationUnits,
                    Is.EqualTo(100));

                Assert.That(
                    runResult.MatchedCount,
                    Is.EqualTo(70));

                Assert.That(
                    runResult.MismatchedCount,
                    Is.EqualTo(10));

                Assert.That(
                    runResult.MissingCount,
                    Is.EqualTo(12));

                Assert.That(
                    runResult.DuplicateCount,
                    Is.EqualTo(6));

                Assert.That(
                    runResult.UnresolvedCount,
                    Is.EqualTo(2));

                Assert.That(
                    runResult.MatchedCount +
                    runResult.MismatchedCount +
                    runResult.MissingCount +
                    runResult.DuplicateCount +
                    runResult.UnresolvedCount,
                    Is.EqualTo(runResult.TotalReconciliationUnits));

                Assert.That(
                    runResult.MatchRate,
                    Is.EqualTo(70.00m));
            });

            // --------------------------------------------------------
            // Map real persisted rows into the comparator's independent
            // wire-shaped DTOs, then run the SAME Compare() logic the
            // console tool uses -- against the real database output.
            // --------------------------------------------------------
            var normalizedTransactions =
                await normalizedTransactionRepository.GetByRunIdAsync(
                    runResult.RunId);

            var referenceByTransactionId =
                normalizedTransactions.ToDictionary(
                    x => x.Id,
                    x => x.TransactionReference);

            var persistedResults =
                await resultRepository.GetByRunIdAsync(
                    runResult.RunId);

            var actualResults =
                persistedResults
                    .Select(
                        x => new ActualResult
                        {
                            ResultId = x.Id,
                            RunId = x.RunId,
                            NormalizedTransactionId =
                                x.NormalizedTransactionId,
                            TransactionReference =
                                referenceByTransactionId[
                                    x.NormalizedTransactionId],
                            Status = x.Status.ToString(),
                            StrategyUsed = x.StrategyUsed,
                            ReasonCode = x.ReasonCode.ToString(),
                            CreatedAt = x.CreatedAt
                        })
                    .ToList();

            var resultById =
                persistedResults.ToDictionary(x => x.Id);

            var persistedExceptions =
                await exceptionRepository.GetByRunIdAsync(
                    runResult.RunId);

            var actualExceptions =
                persistedExceptions
                    .Select(
                        x => new ActualException
                        {
                            ExceptionId = x.Id,
                            RunId = x.RunId,
                            ReconciliationResultId =
                                x.ReconciliationResultId,
                            Category = x.Category.ToString(),
                            TransactionReference =
                                referenceByTransactionId[
                                    resultById[x.ReconciliationResultId]
                                        .NormalizedTransactionId]
                        })
                    .ToList();

            var comparison =
                GroundTruthComparer.Compare(
                    groundTruthRows,
                    actualResults,
                    actualExceptions);

            Assert.That(
                comparison.IsSuccess,
                Is.True,
                "Ground-truth comparison reported discrepancies:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, comparison.Failures));

            Assert.That(comparison.Failures, Is.Empty);
            Assert.That(comparison.ActualMatchRate, Is.EqualTo(70.00m));
            Assert.That(comparison.ExpectedMatchRate, Is.EqualTo(70.00m));

            // --------------------------------------------------------
            // Metric consistency: independently recompute the match
            // rate from the queried results and compare it against the
            // stored ReconciliationRun.MatchRate -- these must agree,
            // not merely both be self-reported by the same code path.
            // --------------------------------------------------------
            var run =
                await runRepository.GetByIdAsync(runResult.RunId);

            Assert.That(run, Is.Not.Null);

            var independentlyRecomputedMatchRate =
                decimal.Round(
                    actualResults.Count(
                        x => x.Status == "Matched") *
                    100.00m /
                    actualResults.Count,
                    2);

            Assert.That(
                run!.MatchRate,
                Is.EqualTo(independentlyRecomputedMatchRate));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task ExecuteAsync_RunTwiceAgainstSameBatch_ProducesIdenticalStatusAndReasonCodeAssignments()
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

        var normalizedTransactionRepository =
            scope.ServiceProvider
                .GetRequiredService<INormalizedTransactionRepository>();

        // A compact fixture spanning several distinct outcomes --
        // matched, mismatched, missing (bank-only orphan), duplicate --
        // so determinism is proven across more than one status.
        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-004001,TXN-4001,1000.00,INR,2026-08-01,COMPLETED
                PAY-004002,TXN-4002,2000.00,INR,2026-08-02,COMPLETED
                PAY-004003,TXN-4003,3000.00,INR,2026-08-03,COMPLETED
                PAY-004004,TXN-4003,3000.00,INR,2026-08-03,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-004001,TXN-4001,1000.00,INR,2026-08-01,CLEARED
                BANK-004002,TXN-4002,1950.00,INR,2026-08-02,CLEARED
                BANK-004003,TXN-4003,3000.00,INR,2026-08-03,CLEARED
                BANK-004004,TXN-4004,750.00,INR,2026-08-04,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-004001,TXN-4001,1000.00,INR,2026-08-01,SETTLED
                SET-004002,TXN-4002,1950.00,INR,2026-08-02,SETTLED
                SET-004003,TXN-4003,3000.00,INR,2026-08-03,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Determinism Test - Repeated Runs",

                    CreatedBy =
                        "integration-test",

                    PaymentFile =
                        paymentsStream,

                    BankFile =
                        bankStream,

                    SettlementFile =
                        settlementStream
                });

        var firstRun =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId = ingestionResult.BatchId
                });

        var secondRun =
            await reconciliationService.ExecuteAsync(
                new ReconciliationRunRequest
                {
                    BatchId = ingestionResult.BatchId
                });

        Assert.That(firstRun.RunId, Is.Not.EqualTo(secondRun.RunId));

        Assert.Multiple(() =>
        {
            Assert.That(
                secondRun.TotalReconciliationUnits,
                Is.EqualTo(firstRun.TotalReconciliationUnits));

            Assert.That(
                secondRun.MatchedCount,
                Is.EqualTo(firstRun.MatchedCount));

            Assert.That(
                secondRun.MismatchedCount,
                Is.EqualTo(firstRun.MismatchedCount));

            Assert.That(
                secondRun.MissingCount,
                Is.EqualTo(firstRun.MissingCount));

            Assert.That(
                secondRun.DuplicateCount,
                Is.EqualTo(firstRun.DuplicateCount));

            Assert.That(
                secondRun.UnresolvedCount,
                Is.EqualTo(firstRun.UnresolvedCount));

            Assert.That(
                secondRun.MatchRate,
                Is.EqualTo(firstRun.MatchRate));
        });

        var firstRunByReference =
            await BuildStatusByReferenceAsync(
                firstRun.RunId,
                resultRepository,
                normalizedTransactionRepository);

        var secondRunByReference =
            await BuildStatusByReferenceAsync(
                secondRun.RunId,
                resultRepository,
                normalizedTransactionRepository);

        Assert.That(
            secondRunByReference.Count,
            Is.EqualTo(firstRunByReference.Count));

        foreach (var (reference, first) in firstRunByReference)
        {
            Assert.That(
                secondRunByReference.TryGetValue(reference, out var second),
                Is.True,
                $"{reference} present in first run but not in second run.");

            Assert.That(
                second.Status,
                Is.EqualTo(first.Status),
                $"{reference}: Status differs between run 1 and run 2.");

            Assert.That(
                second.ReasonCode,
                Is.EqualTo(first.ReasonCode),
                $"{reference}: ReasonCode differs between run 1 and run 2.");
        }
    }

    /// <summary>
    /// Phase 9: proves only that the additive timing fields exist on a real,
    /// persisted ReconciliationCompleted audit log and hold sane (non-negative,
    /// numeric) values -- NOT a performance assertion, and no specific
    /// duration/throughput value is asserted, so this cannot become flaky
    /// under real timing variance.
    /// </summary>
    [Test]
    public async Task ExecuteAsync_OnSuccess_RecordsDurationAndThroughputInCompletedAuditLog()
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

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-005001,TXN-5001,1000.00,INR,2026-08-01,COMPLETED
                PAY-005002,TXN-5002,2000.00,INR,2026-08-02,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-005001,TXN-5001,1000.00,INR,2026-08-01,CLEARED
                BANK-005002,TXN-5002,2000.00,INR,2026-08-02,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-005001,TXN-5001,1000.00,INR,2026-08-01,SETTLED
                SET-005002,TXN-5002,2000.00,INR,2026-08-02,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Phase 9 Timing Audit Test",

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
                    BatchId = ingestionResult.BatchId
                });

        var completedAuditLog =
            await dbContext.AuditLogs
                .Where(
                    x =>
                        x.RunId == runResult.RunId &&
                        x.EventType == AuditEventType.ReconciliationCompleted)
                .SingleOrDefaultAsync();

        Assert.That(
            completedAuditLog,
            Is.Not.Null,
            "No ReconciliationCompleted audit log was persisted for this run.");

        using var payload =
            JsonDocument.Parse(completedAuditLog!.DetailPayload);

        var root = payload.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(
                root.TryGetProperty("duration_ms", out var durationProperty),
                Is.True,
                "duration_ms is missing from the ReconciliationCompleted audit payload.");

            Assert.That(
                durationProperty.ValueKind,
                Is.EqualTo(JsonValueKind.Number),
                "duration_ms must be a JSON number.");

            Assert.That(
                durationProperty.GetInt64(),
                Is.GreaterThanOrEqualTo(0),
                "duration_ms must not be negative.");

            Assert.That(
                root.TryGetProperty("records_per_second", out var throughputProperty),
                Is.True,
                "records_per_second is missing from the ReconciliationCompleted audit payload.");

            Assert.That(
                throughputProperty.ValueKind,
                Is.EqualTo(JsonValueKind.Number),
                "records_per_second must be a JSON number.");

            Assert.That(
                throughputProperty.GetDouble(),
                Is.GreaterThanOrEqualTo(0),
                "records_per_second must not be negative.");
        });

        // The existing, unchanged aggregate fields must still be present and
        // correct alongside the two new ones -- proving this is additive,
        // not a replacement of the prior payload shape.
        Assert.Multiple(() =>
        {
            Assert.That(
                root.GetProperty("total_units").GetInt32(),
                Is.EqualTo(runResult.TotalReconciliationUnits));

            Assert.That(
                root.GetProperty("match_rate").GetDecimal(),
                Is.EqualTo(runResult.MatchRate));
        });
    }

    private static async Task<Dictionary<string, (string Status, string ReasonCode)>>
        BuildStatusByReferenceAsync(
            Guid runId,
            IReconciliationResultRepository resultRepository,
            INormalizedTransactionRepository normalizedTransactionRepository)
    {
        var normalizedTransactions =
            await normalizedTransactionRepository.GetByRunIdAsync(runId);

        var referenceByTransactionId =
            normalizedTransactions.ToDictionary(
                x => x.Id,
                x => x.TransactionReference);

        var results =
            await resultRepository.GetByRunIdAsync(runId);

        return results.ToDictionary(
            x => referenceByTransactionId[x.NormalizedTransactionId],
            x => (x.Status.ToString(), x.ReasonCode.ToString()),
            StringComparer.Ordinal);
    }

    private static FileStream OpenRead(
        string directory,
        string fileName)
    {
        return new FileStream(
            Path.Combine(directory, fileName),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(content));
    }
}
