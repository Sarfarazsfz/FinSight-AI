using System.Text.Json;
using FinSight.Application.AI;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class ExceptionDetailsToolTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task GetExceptionDetails_ReturnsAuthoritativeExceptionData()
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

        var exceptionRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var resultRepository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IExceptionDetailsTool>();

        await using var paymentsStream =
            CreateStream(
                """
                payment_record_id,transaction_reference,amount,currency,transaction_date,payment_status
                PAY-000001,TXN-0001,1000.00,INR,2026-08-01,COMPLETED
                PAY-000002,TXN-0002,2000.00,INR,2026-08-02,COMPLETED
                PAY-000003,TXN-0003,3000.00,INR,2026-08-03,COMPLETED
                """);

        await using var bankStream =
            CreateStream(
                """
                bank_record_id,transaction_reference,amount,currency,transaction_date,bank_status
                BANK-000001,TXN-0001,1000.00,INR,2026-08-01,CLEARED
                BANK-000002,TXN-0002,1990.00,INR,2026-08-02,CLEARED
                BANK-000003,TXN-0003,3000.00,INR,2026-08-03,CLEARED
                """);

        await using var settlementStream =
            CreateStream(
                """
                settlement_record_id,transaction_reference,amount,currency,transaction_date,settlement_status
                SET-000001,TXN-0001,1000.00,INR,2026-08-01,SETTLED
                SET-000002,TXN-0002,1990.00,INR,2026-08-02,SETTLED
                SET-000003,TXN-0003,3000.00,INR,2026-08-03,SETTLED
                """);

        var ingestionResult =
            await ingestionService.IngestAsync(
                new BatchIngestionRequest
                {
                    BatchLabel = "AI Exception Tool Test",
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

        Assert.That(
            runResult.Status,
            Is.EqualTo(ReconciliationRunStatus.Completed));

        var persistedExceptions =
            await exceptionRepository.GetByRunIdAsync(
                runResult.RunId);

        Assert.That(
            persistedExceptions,
            Is.Not.Empty);

        var targetException =
            persistedExceptions[0];

        var persistedResult =
            await resultRepository.GetByIdAsync(
                targetException.ReconciliationResultId);

        Assert.That(
            persistedResult,
            Is.Not.Null);

        var toolResult =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    ExceptionId =
                        targetException.Id
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                toolResult.Success,
                Is.True);

            Assert.That(
                toolResult.ToolName,
                Is.EqualTo("getExceptionDetails"));

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

        var response =
            JsonSerializer.Deserialize<
                ReconciliationExceptionResponse>(
                toolResult.DataJson,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.That(
            response,
            Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                response!.ExceptionId,
                Is.EqualTo(targetException.Id));

            Assert.That(
                response.RunId,
                Is.EqualTo(targetException.RunId));

            Assert.That(
                response.ReconciliationResultId,
                Is.EqualTo(
                    targetException.ReconciliationResultId));

            Assert.That(
                response.Category,
                Is.EqualTo(
                    targetException.Category.ToString()));

            Assert.That(
                response.InvolvedSources,
                Is.EqualTo(
                    targetException.InvolvedSources));

            using var expectedDiscrepancy =
                JsonDocument.Parse(
                    targetException.DiscrepancyDetail);

            using var actualDiscrepancy =
                JsonDocument.Parse(
                    response!.DiscrepancyDetail);

            AssertJsonEquivalent(
                expectedDiscrepancy.RootElement,
                actualDiscrepancy.RootElement);

            Assert.That(
                response.AiExplanation,
                Is.EqualTo(
                    targetException.AiExplanation));

            Assert.That(
                response.AiSuggestedCategory,
                Is.EqualTo(
                    targetException.AiSuggestedCategory));

            Assert.That(
                response.CreatedAt,
                Is.EqualTo(
                    targetException.CreatedAt)
                    .Within(
                        TimeSpan.FromTicks(
                            TimeSpan.TicksPerMillisecond)));

            Assert.That(
                response.UpdatedAt,
                Is.EqualTo(
                    targetException.UpdatedAt));
        });
    }

    [Test]
    public async Task GetExceptionDetails_WithInvalidExceptionId_ReturnsInvalidArgument()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IExceptionDetailsTool>();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Success,
                Is.False);

            Assert.That(
                result.ToolName,
                Is.EqualTo("getExceptionDetails"));

            Assert.That(
                result.ErrorCode,
                Is.EqualTo("INVALID_ARGUMENT"));
        });
    }

    [Test]
    public async Task GetExceptionDetails_WithUnknownException_ReturnsExceptionNotFound()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var tool =
            scope.ServiceProvider
                .GetRequiredService<IExceptionDetailsTool>();

        var result =
            await tool.ExecuteAsync(
                new FinanceToolRequest
                {
                    ExceptionId = Guid.NewGuid()
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Success,
                Is.False);

            Assert.That(
                result.ToolName,
                Is.EqualTo("getExceptionDetails"));

            Assert.That(
                result.ErrorCode,
                Is.EqualTo("EXCEPTION_NOT_FOUND"));
        });
    }

    private static void AssertJsonEquivalent(
        JsonElement expected,
        JsonElement actual)
    {
        Assert.That(
            actual.ValueKind,
            Is.EqualTo(expected.ValueKind));

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var expectedProperties =
                    expected.EnumerateObject()
                        .ToDictionary(
                            x => x.Name,
                            x => x.Value);

                var actualProperties =
                    actual.EnumerateObject()
                        .ToDictionary(
                            x => x.Name,
                            x => x.Value);

                Assert.That(
                    actualProperties.Keys,
                    Is.EquivalentTo(
                        expectedProperties.Keys));

                foreach (var property in expectedProperties)
                {
                    AssertJsonEquivalent(
                        property.Value,
                        actualProperties[property.Key]);
                }

                break;
            }

            case JsonValueKind.Array:
            {
                var expectedItems =
                    expected.EnumerateArray().ToList();

                var actualItems =
                    actual.EnumerateArray().ToList();

                Assert.That(
                    actualItems.Count,
                    Is.EqualTo(expectedItems.Count));

                for (var i = 0; i < expectedItems.Count; i++)
                {
                    AssertJsonEquivalent(
                        expectedItems[i],
                        actualItems[i]);
                }

                break;
            }

            case JsonValueKind.String:
                Assert.That(
                    actual.GetString(),
                    Is.EqualTo(
                        expected.GetString()));
                break;

            case JsonValueKind.Number:
                Assert.That(
                    actual.GetRawText(),
                    Is.EqualTo(
                        expected.GetRawText()));
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.That(
                    actual.GetBoolean(),
                    Is.EqualTo(
                        expected.GetBoolean()));
                break;

            case JsonValueKind.Null:
                break;
        }
    }



    private static MemoryStream CreateStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content.TrimStart()));
    }
}
