using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

[TestFixture]
[NonParallelizable]
public sealed class ReconciliationPaginationIntegrationTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture =
            new PostgresIntegrationFixture();
    }

    [Test]
    public async Task ResultRepository_GetPageByRunId_ReturnsRequestedPageAndTotalCount()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<FinSight.Infrastructure.Persistence.AppDbContext>();

        var batch =
            new Batch(
                "Pagination Test Batch - Results",
                0,
                0,
                0,
                "Valid",
                "pagination-test");

        dbContext.Batches.Add(batch);

        var run =
            new ReconciliationRun(batch.Id);

        dbContext.ReconciliationRuns.Add(run);

        for (var i = 1; i <= 5; i++)
        {
            var normalizedTransaction =
                new NormalizedTransaction(
                    run.Id,
                    $"PAG-RESULT-{i:D3}",
                    null,
                    null,
                    null);

            dbContext.NormalizedTransactions.Add(
                normalizedTransaction);

            dbContext.ReconciliationResults.Add(
                new ReconciliationResult(
                    run.Id,
                    normalizedTransaction.Id,
                    MatchStatus.Matched,
                    ReconciliationReasonCode.EXACT_MATCH,
                    "TestStrategy"));
        }

        await dbContext.SaveChangesAsync();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationResultRepository>();

        var page =
            await repository.GetPageByRunIdAsync(
                run.Id,
                pageNumber: 2,
                pageSize: 2);

        Assert.That(
            page.TotalCount,
            Is.EqualTo(5));

        Assert.That(
            page.Items,
            Has.Count.EqualTo(2));

        var allResults =
            await repository.GetByRunIdAsync(
                run.Id);

        Assert.That(
            page.Items[0].Id,
            Is.EqualTo(allResults[2].Id));

        Assert.That(
            page.Items[1].Id,
            Is.EqualTo(allResults[3].Id));
    }

    [Test]
    public async Task ExceptionRepository_GetPageByRunId_ReturnsRequestedPageAndTotalCount()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<FinSight.Infrastructure.Persistence.AppDbContext>();

        var batch =
            new Batch(
                "Pagination Test Batch - Exceptions",
                0,
                0,
                0,
                "Valid",
                "pagination-test");

        dbContext.Batches.Add(batch);

        var run =
            new ReconciliationRun(batch.Id);

        dbContext.ReconciliationRuns.Add(run);

        var results =
            new List<ReconciliationResult>();

        for (var i = 1; i <= 5; i++)
        {
            var normalizedTransaction =
                new NormalizedTransaction(
                    run.Id,
                    $"PAG-EXCEPTION-{i:D3}",
                    null,
                    null,
                    null);

            dbContext.NormalizedTransactions.Add(
                normalizedTransaction);

            var result =
                new ReconciliationResult(
                    run.Id,
                    normalizedTransaction.Id,
                    MatchStatus.Mismatched,
                    ReconciliationReasonCode.AMOUNT_MISMATCH,
                    "TestStrategy");

            dbContext.ReconciliationResults.Add(result);

            results.Add(result);
        }

        await dbContext.SaveChangesAsync();

        foreach (var result in results)
        {
            dbContext.ReconciliationExceptions.Add(
                new ReconciliationException(
                    run.Id,
                    result.Id,
                    ExceptionCategory.AmountMismatch,
                    "Payment,Bank,Settlement",
                    "{\"message\":\"Test discrepancy\"}"));
        }

        await dbContext.SaveChangesAsync();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IReconciliationExceptionRepository>();

        var page =
            await repository.GetPageByRunIdAsync(
                run.Id,
                pageNumber: 2,
                pageSize: 2);

        Assert.That(
            page.TotalCount,
            Is.EqualTo(5));

        Assert.That(
            page.Items,
            Has.Count.EqualTo(2));

        var allExceptions =
            await repository.GetByRunIdAsync(
                run.Id);

        Assert.That(
            page.Items[0].Id,
            Is.EqualTo(allExceptions[2].Id));

        Assert.That(
            page.Items[1].Id,
            Is.EqualTo(allExceptions[3].Id));
    }
}
