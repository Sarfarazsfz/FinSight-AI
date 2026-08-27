using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Evaluation;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Phase 3: proves ReconciliationController.GetSummary and the Finance
/// Assistant's getReconciliationSummary tool -- previously two
/// independent, line-for-line duplicated implementations -- now return
/// identical numbers for the same run, because both consume the same
/// IReconciliationSummaryBuilder.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ReconciliationSummaryConsistencyTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task ControllerAndTool_ReturnIdenticalSummaryNumbers_ForTheSameRun()
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
                BANK-005002,TXN-5002,1950.00,INR,2026-08-02,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-005001,TXN-5001,1000.00,INR,2026-08-01,SETTLED
                SET-005002,TXN-5002,1950.00,INR,2026-08-02,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel =
                        "Summary Consistency Test",

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

        // Tool path.
        var summaryTool =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationSummaryTool>();

        var toolResult =
            await summaryTool.ExecuteAsync(
                new FinanceToolRequest
                {
                    RunId = runResult.RunId
                });

        Assert.That(toolResult.Success, Is.True);

        var toolSummary =
            System.Text.Json.JsonSerializer.Deserialize<
                ReconciliationRunSummaryResponse>(
                toolResult.DataJson!);

        Assert.That(toolSummary, Is.Not.Null);

        // Controller path -- constructed directly (not registered as a
        // DI service), pulling every dependency from the same scope.
        var controller =
            new ReconciliationController(
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationService>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationRunRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationResultRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationExceptionRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<INormalizedTransactionRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IPaymentRecordRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IBankRecordRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<ISettlementRecordRepository>(),
                new FakeAiExplanationService(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationSummaryBuilder>(),
                scope.ServiceProvider
                    .GetRequiredService<IGroundTruthComparisonService>());

        var controllerActionResult =
            await controller.GetSummary(
                runResult.RunId,
                CancellationToken.None);

        var controllerSummary =
            (controllerActionResult.Result as OkObjectResult)?.Value
                as ReconciliationRunSummaryResponse;

        Assert.That(controllerSummary, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                controllerSummary!.RunId,
                Is.EqualTo(toolSummary!.RunId));

            Assert.That(
                controllerSummary.BatchId,
                Is.EqualTo(toolSummary.BatchId));

            Assert.That(
                controllerSummary.Status,
                Is.EqualTo(toolSummary.Status));

            Assert.That(
                controllerSummary.TotalUnits,
                Is.EqualTo(toolSummary.TotalUnits));

            Assert.That(
                controllerSummary.Matched,
                Is.EqualTo(toolSummary.Matched));

            Assert.That(
                controllerSummary.Mismatched,
                Is.EqualTo(toolSummary.Mismatched));

            Assert.That(
                controllerSummary.Missing,
                Is.EqualTo(toolSummary.Missing));

            Assert.That(
                controllerSummary.Duplicate,
                Is.EqualTo(toolSummary.Duplicate));

            Assert.That(
                controllerSummary.Unresolved,
                Is.EqualTo(toolSummary.Unresolved));

            Assert.That(
                controllerSummary.MatchRate,
                Is.EqualTo(toolSummary.MatchRate));

            Assert.That(
                controllerSummary.ExceptionCount,
                Is.EqualTo(toolSummary.ExceptionCount));
        });
    }

    [Test]
    public async Task Controller_WithUnknownRunId_ReturnsProblemDetails404()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var controller =
            new ReconciliationController(
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationService>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationRunRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationResultRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationExceptionRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<INormalizedTransactionRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IPaymentRecordRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<IBankRecordRepository>(),
                scope.ServiceProvider
                    .GetRequiredService<ISettlementRecordRepository>(),
                new FakeAiExplanationService(),
                scope.ServiceProvider
                    .GetRequiredService<IReconciliationSummaryBuilder>(),
                scope.ServiceProvider
                    .GetRequiredService<IGroundTruthComparisonService>());

        var result =
            await controller.GetSummary(
                Guid.NewGuid(),
                CancellationToken.None);

        var objectResult =
            result.Result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status404NotFound));

        Assert.That(
            objectResult.Value,
            Is.InstanceOf<ProblemDetails>());
    }

    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content.TrimStart()));
    }

    /// <summary>
    /// These tests exercise GetSummary only, which never touches AI
    /// explanation -- resolving the REAL IAiExplanationService from DI
    /// just to satisfy ReconciliationController's constructor would
    /// incidentally construct the whole AI-explanation graph down to
    /// OpenAiProvider, which validates its API key eagerly and throws
    /// when none is configured (deliberately absent from
    /// PostgresIntegrationFixture's test-only configuration). This fake
    /// is never invoked by either test.
    /// </summary>
    private sealed class FakeAiExplanationService
        : IAiExplanationService
    {
        public Task<AiExplanationResponse> ExplainAsync(
            Guid exceptionId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Not used by these tests -- GetSummary does not " +
                "generate AI explanations.");
        }
    }
}
