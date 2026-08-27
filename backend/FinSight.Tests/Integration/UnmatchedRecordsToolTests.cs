using FinSight.Application.DTOs.Reconciliation;
using System.Text.Json;
using FinSight.Application.AI;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class UnmatchedRecordsToolTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task GetUnmatchedRecords_ReturnsEveryNonMatchedResult()
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

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IUnmatchedRecordsTool>();

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
                        "AI Unmatched Tool Test",

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
            runResult.Status,
            Is.EqualTo(ReconciliationRunStatus.Completed));

        var persistedResults =
            await resultRepository.GetByRunIdAsync(
                runResult.RunId);

        var expectedUnmatched =
            persistedResults
                .Where(x => x.Status != MatchStatus.Matched)
                .ToList();

        var toolResult =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId =
                        runResult.RunId
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                toolResult.Success,
                Is.True);

            Assert.That(
                toolResult.ToolName,
                Is.EqualTo("getUnmatchedRecords"));

            Assert.That(
                toolResult.ErrorCode,
                Is.Null);

            Assert.That(
                toolResult.ErrorMessage,
                Is.Null);

            Assert.That(
                toolResult.DataJson,
                Is.Not.Null.And.Not.Empty);
        });

        using var json =
            JsonDocument.Parse(
                toolResult.DataJson);

        var root =
            json.RootElement;

        Assert.That(
            root.GetProperty("runId").GetGuid(),
            Is.EqualTo(runResult.RunId));

        Assert.That(
            root.GetProperty("totalUnmatched").GetInt32(),
            Is.EqualTo(expectedUnmatched.Count));

        var items =
            root.GetProperty("items");

        Assert.That(
            items.GetArrayLength(),
            Is.EqualTo(expectedUnmatched.Count));

        foreach (var item in items.EnumerateArray())
        {
            Assert.That(
                item.GetProperty("status").GetString(),
                Is.Not.EqualTo("Matched"));
        }
    }

    [Test]
    public async Task GetUnmatchedRecords_WithInvalidRunId_ReturnsInvalidArgument()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IUnmatchedRecordsTool>();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("INVALID_ARGUMENT"));
            Assert.That(
                result.ToolName,
                Is.EqualTo("getUnmatchedRecords"));
        });
    }

    [Test]
    public async Task GetUnmatchedRecords_WithUnknownRunId_ReturnsRunNotFound()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IUnmatchedRecordsTool>();

        var unknownRunId =
            Guid.NewGuid();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId = unknownRunId
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("RUN_NOT_FOUND"));
            Assert.That(
                result.ToolName,
                Is.EqualTo("getUnmatchedRecords"));
        });
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content.TrimStart()));
    }
}
