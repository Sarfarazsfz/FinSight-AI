using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Application.AI;
using FinSight.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class TransactionDetailsToolTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task GetTransactionDetails_ReturnsAuthoritativeSourceDetails()
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

        var normalizedRepository =
            scope.ServiceProvider
                .GetRequiredService<INormalizedTransactionRepository>();

        var paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRecordRepository>();

        var bankRepository =
            scope.ServiceProvider
                .GetRequiredService<IBankRecordRepository>();

        var settlementRepository =
            scope.ServiceProvider
                .GetRequiredService<ISettlementRecordRepository>();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<ITransactionDetailsTool>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-000001,TXN-0001,1000.00,INR,2026-08-01,COMPLETED
                PAY-000002,TXN-0002,2000.00,INR,2026-08-02,COMPLETED
                PAY-000003,TXN-0003,3000.00,INR,2026-08-03,COMPLETED
                PAY-000004,TXN-0004,4000.00,INR,2026-08-04,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-000001,TXN-0001,1000.00,INR,2026-08-01,CLEARED
                BANK-000002,TXN-0002,2000.00,INR,2026-08-02,CLEARED
                BANK-000003,TXN-0003,2990.00,INR,2026-08-03,CLEARED
                BANK-000004,TXN-0004,4000.00,INR,2026-08-04,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-000001,TXN-0001,1000.00,INR,2026-08-01,SETTLED
                SET-000002,TXN-0002,2000.00,INR,2026-08-02,SETTLED
                SET-000003,TXN-0003,2990.00,INR,2026-08-03,SETTLED
                SET-000004,TXN-0004,4000.00,INR,2026-08-04,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "AI Transaction Detail Tool Test",
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

        Assert.That(
            runResult.Status,
            Is.EqualTo(ReconciliationRunStatus.Completed));

        var results =
            await resultRepository.GetByRunIdAsync(
                runResult.RunId);

        var targetResult =
            results.First();

        var normalized =
            await normalizedRepository.GetByIdAsync(
                targetResult.NormalizedTransactionId);

        Assert.That(normalized, Is.Not.Null);

        var payments =
            await paymentRepository.GetByBatchIdAsync(
                ingestionResult.BatchId);

        var banks =
            await bankRepository.GetByBatchIdAsync(
                ingestionResult.BatchId);

        var settlements =
            await settlementRepository.GetByBatchIdAsync(
                ingestionResult.BatchId);

        var expectedPayments =
            payments
                .Where(x =>
                    string.Equals(
                        x.TransactionReference,
                        normalized!.TransactionReference,
                        StringComparison.Ordinal))
                .ToList();

        var expectedBanks =
            banks
                .Where(x =>
                    string.Equals(
                        x.TransactionReference,
                        normalized!.TransactionReference,
                        StringComparison.Ordinal))
                .ToList();

        var expectedSettlements =
            settlements
                .Where(x =>
                    string.Equals(
                        x.TransactionReference,
                        normalized!.TransactionReference,
                        StringComparison.Ordinal))
                .ToList();

        var toolResult =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId = runResult.RunId,
                    ResultId = targetResult.Id
                });

        Assert.Multiple(() =>
        {
            Assert.That(toolResult.Success, Is.True);
            Assert.That(
                toolResult.ToolName,
                Is.EqualTo("getTransactionDetails"));
            Assert.That(
                toolResult.ErrorCode,
                Is.Null);
            Assert.That(
                toolResult.DataJson,
                Is.Not.Null.And.Not.Empty);
        });

        var response =
            JsonSerializer.Deserialize<
                ReconciliationTransactionDetailResponse>(
                toolResult.DataJson,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.That(response, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                response!.ResultId,
                Is.EqualTo(targetResult.Id));

            Assert.That(
                response.RunId,
                Is.EqualTo(runResult.RunId));

            Assert.That(
                response.NormalizedTransactionId,
                Is.EqualTo(targetResult.NormalizedTransactionId));

            Assert.That(
                response.TransactionReference,
                Is.EqualTo(normalized!.TransactionReference));

            Assert.That(
                response.Status,
                Is.EqualTo(targetResult.Status.ToString()));

            Assert.That(
                response.ReasonCode,
                Is.EqualTo(targetResult.ReasonCode.ToString()));

            Assert.That(
                response.Payments.Count,
                Is.EqualTo(expectedPayments.Count));

            Assert.That(
                response.Banks.Count,
                Is.EqualTo(expectedBanks.Count));

            Assert.That(
                response.Settlements.Count,
                Is.EqualTo(expectedSettlements.Count));
        });
    }

    [Test]
    public async Task GetTransactionDetails_WithInvalidArguments_ReturnsInvalidArgument()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<ITransactionDetailsTool>();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ToolName,
                Is.EqualTo("getTransactionDetails"));
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("INVALID_ARGUMENT"));
        });
    }

    [Test]
    public async Task GetTransactionDetails_WithUnknownRun_ReturnsRunNotFound()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<ITransactionDetailsTool>();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId = Guid.NewGuid(),
                    ResultId = Guid.NewGuid()
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ToolName,
                Is.EqualTo("getTransactionDetails"));
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("RUN_NOT_FOUND"));
        });
    }

    [Test]
    public async Task GetTransactionDetails_WithUnknownResult_ReturnsResultNotFound()
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

        var tool =
            scope.ServiceProvider
                .GetRequiredService<ITransactionDetailsTool>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-000001,TXN-0001,1000.00,INR,2026-08-01,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-000001,TXN-0001,1000.00,INR,2026-08-01,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-000001,TXN-0001,1000.00,INR,2026-08-01,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel = "Unknown Result Test",
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

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId = runResult.RunId,
                    ResultId = Guid.NewGuid()
                });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(
                result.ErrorCode,
                Is.EqualTo("RESULT_NOT_FOUND"));
            Assert.That(
                result.ToolName,
                Is.EqualTo("getTransactionDetails"));
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
