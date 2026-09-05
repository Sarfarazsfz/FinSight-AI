using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Tests.Integration;

/// <summary>
/// Phase 4A.2 (Batch History): proves IBatchRepository.GetPageAsync's
/// ordering/pagination correctness at the repository level (mirroring
/// ReconciliationPaginationIntegrationTests' exact pattern), and
/// BatchesController.GetBatches's validation behavior via direct
/// construction (mirroring ReconciliationSummaryConsistencyTests'
/// convention) -- everything except [Authorize] enforcement itself,
/// which requires a real HTTP pipeline (see BatchesAuthorizationTests).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class BatchPaginationIntegrationTests
{
    private PostgresIntegrationFixture _fixture = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixture = new PostgresIntegrationFixture();
    }

    [Test]
    public async Task GetPageAsync_ReturnsEmptyResult_WhenNoBatchesExist()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IBatchRepository>();

        var page =
            await repository.GetPageAsync(
                pageNumber: 1,
                pageSize: 50);

        Assert.That(page.TotalCount, Is.EqualTo(0));
        Assert.That(page.Items, Is.Empty);
    }

    [Test]
    public async Task GetPageAsync_ReturnsNewestFirst_WithStableIdTieBreak()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    FinSight.Infrastructure.Persistence.AppDbContext>();

        // Three distinct CreatedAt values (oldest to newest) plus two
        // batches sharing the SAME CreatedAt as the newest, to prove the
        // Id DESC tie-break is genuinely applied, not just incidental.
        var oldest =
            new Batch(
                "Batch Oldest", 0, 0, 0, "Valid", "pagination-test");

        var middle =
            new Batch(
                "Batch Middle", 0, 0, 0, "Valid", "pagination-test");

        var newestA =
            new Batch(
                "Batch Newest A", 0, 0, 0, "Valid", "pagination-test");

        var newestB =
            new Batch(
                "Batch Newest B", 0, 0, 0, "Valid", "pagination-test");

        dbContext.Batches.AddRange(oldest, middle, newestA, newestB);
        await dbContext.SaveChangesAsync();

        // Force explicit CreatedAt values via raw SQL update, since the
        // entity sets CreatedAt = UtcNow internally with no setter --
        // this is the only way to make two rows share an identical
        // timestamp deterministically for the tie-break assertion.
        var baseTime = DateTime.UtcNow.AddDays(-10);

        await SetCreatedAtAsync(dbContext, oldest.Id, baseTime);
        await SetCreatedAtAsync(dbContext, middle.Id, baseTime.AddHours(1));
        await SetCreatedAtAsync(dbContext, newestA.Id, baseTime.AddHours(2));
        await SetCreatedAtAsync(dbContext, newestB.Id, baseTime.AddHours(2));

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IBatchRepository>();

        var page =
            await repository.GetPageAsync(
                pageNumber: 1,
                pageSize: 10);

        Assert.That(page.TotalCount, Is.EqualTo(4));
        Assert.That(page.Items, Has.Count.EqualTo(4));

        // newestA/newestB share a CreatedAt -- whichever has the larger
        // Id must sort first (Id DESC tie-break), then middle, then
        // oldest last.
        var expectedFirstTwoIds =
            new[] { newestA.Id, newestB.Id }
                .OrderByDescending(id => id)
                .ToArray();

        Assert.That(
            page.Items[0].Id,
            Is.EqualTo(expectedFirstTwoIds[0]));

        Assert.That(
            page.Items[1].Id,
            Is.EqualTo(expectedFirstTwoIds[1]));

        Assert.That(page.Items[2].Id, Is.EqualTo(middle.Id));
        Assert.That(page.Items[3].Id, Is.EqualTo(oldest.Id));
    }

    [Test]
    public async Task GetPageAsync_ReturnsRequestedPageAndTotalCount()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    FinSight.Infrastructure.Persistence.AppDbContext>();

        var baseTime = DateTime.UtcNow.AddDays(-1);
        var batches = new List<Batch>();

        for (var i = 1; i <= 5; i++)
        {
            var batch =
                new Batch(
                    $"Pagination Batch {i:D2}",
                    0, 0, 0, "Valid", "pagination-test");

            dbContext.Batches.Add(batch);
            batches.Add(batch);
        }

        await dbContext.SaveChangesAsync();

        for (var i = 0; i < batches.Count; i++)
        {
            await SetCreatedAtAsync(
                dbContext,
                batches[i].Id,
                baseTime.AddMinutes(i));
        }

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IBatchRepository>();

        var page =
            await repository.GetPageAsync(
                pageNumber: 2,
                pageSize: 2);

        Assert.That(page.TotalCount, Is.EqualTo(5));
        Assert.That(page.Items, Has.Count.EqualTo(2));

        // Newest-first: batches[4] (latest CreatedAt) is page 1 item 1,
        // batches[3] is page 1 item 2, so page 2 starts at batches[2].
        Assert.That(page.Items[0].Id, Is.EqualTo(batches[2].Id));
        Assert.That(page.Items[1].Id, Is.EqualTo(batches[1].Id));
    }

    [Test]
    public async Task GetBatches_WithPageNumberLessThanOne_Returns400()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var controller = CreateController(scope);

        var result =
            await controller.GetBatches(
                pageNumber: 0,
                pageSize: 50,
                CancellationToken.None);

        AssertBadRequest(result);
    }

    [Test]
    public async Task GetBatches_WithPageSizeLessThanOne_Returns400()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var controller = CreateController(scope);

        var result =
            await controller.GetBatches(
                pageNumber: 1,
                pageSize: 0,
                CancellationToken.None);

        AssertBadRequest(result);
    }

    [Test]
    public async Task GetBatches_WithPageSizeGreaterThan100_Returns400()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var controller = CreateController(scope);

        var result =
            await controller.GetBatches(
                pageNumber: 1,
                pageSize: 101,
                CancellationToken.None);

        AssertBadRequest(result);
    }

    [Test]
    public async Task GetBatches_WithNoBatches_Returns200WithEmptyItems()
    {
        await _fixture.ResetDatabaseAsync();

        await using var scope =
            _fixture.CreateScope();

        var controller = CreateController(scope);

        var result =
            await controller.GetBatches(
                pageNumber: 1,
                pageSize: 50,
                CancellationToken.None);

        var okResult =
            result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);

        var body =
            okResult!.Value
                as FinSight.Application.DTOs.Reconciliation
                    .PagedResponse<FinSight.Application.DTOs.Ingestion
                        .BatchResponse>;

        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Items, Is.Empty);
        Assert.That(body.TotalCount, Is.EqualTo(0));
    }

    private static void AssertBadRequest(
        ActionResult<
            FinSight.Application.DTOs.Reconciliation
                .PagedResponse<FinSight.Application.DTOs.Ingestion
                    .BatchResponse>> result)
    {
        var objectResult =
            result.Result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    private static BatchesController CreateController(
        Microsoft.Extensions.DependencyInjection.AsyncServiceScope scope,
        Guid? currentUserId = null)
    {
        // The four tests using this helper are pure input-validation /
        // empty-result checks -- which user is "current" never affects
        // their outcome, so a fresh id is enough when the caller doesn't
        // care.
        return new BatchesController(
            scope.ServiceProvider
                .GetRequiredService<IBatchIngestionService>(),
            scope.ServiceProvider
                .GetRequiredService<IBatchRepository>(),
            new FinSight.Tests.Authorization.FixedCurrentUserService(
                currentUserId ?? Guid.NewGuid()),
            scope.ServiceProvider
                .GetRequiredService<IBatchAccessService>());
    }

    private static async Task SetCreatedAtAsync(
        FinSight.Infrastructure.Persistence.AppDbContext dbContext,
        Guid batchId,
        DateTime createdAt)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE batches SET created_at = {createdAt} WHERE id = {batchId}");
    }
}
