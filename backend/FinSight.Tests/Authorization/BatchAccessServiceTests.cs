using FinSight.Application.Abstractions.Persistence;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Authorization;

namespace FinSight.Tests.Authorization;

/// <summary>
/// The ownership boundary itself, in isolation: every controller-level
/// ownership check in the API ultimately reduces to these two methods, so
/// their correctness is what everything else depends on.
///
/// Pure unit tests against fakes -- no database, not gated by
/// FINSIGHT_TEST_CONNECTION -- because the behaviour under test is the
/// comparison logic (does CreatedByUserId match the caller?), not
/// persistence.
/// </summary>
[TestFixture]
public sealed class BatchAccessServiceTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    private static Batch OwnedBatch() =>
        new(
            "Owned Batch",
            0, 0, 0,
            "Valid",
            "owner@example.test",
            createdByUserId: OwnerUserId);

    private static Batch UnownedLegacyBatch() =>
        new(
            "Legacy Batch",
            0, 0, 0,
            "Valid",
            "legacy-createdby-string");
    // createdByUserId omitted -- exactly the pre-ownership/unmatched-
    // backfill state.

    // --------------------------------------------------------- GetOwnedBatchAsync

    [Test]
    public async Task GetOwnedBatchAsync_WhenTheCallerIsTheOwner_ReturnsTheBatch()
    {
        var batch = OwnedBatch();
        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository());

        var result =
            await service.GetOwnedBatchAsync(batch.Id, OwnerUserId);

        Assert.That(result, Is.SameAs(batch));
    }

    [Test]
    public async Task GetOwnedBatchAsync_WhenTheCallerIsNotTheOwner_ReturnsNull()
    {
        var batch = OwnedBatch();
        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository());

        var result =
            await service.GetOwnedBatchAsync(batch.Id, OtherUserId);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetOwnedBatchAsync_WhenTheBatchIsUnowned_ReturnsNullForEveryCaller()
    {
        // A null CreatedByUserId must never accidentally match a real
        // user id -- this is the safe-default-deny behaviour backfilled
        // legacy data relies on.
        var batch = UnownedLegacyBatch();
        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository());

        Assert.That(
            await service.GetOwnedBatchAsync(batch.Id, OwnerUserId),
            Is.Null);

        Assert.That(
            await service.GetOwnedBatchAsync(batch.Id, OtherUserId),
            Is.Null);
    }

    [Test]
    public async Task GetOwnedBatchAsync_WhenTheBatchDoesNotExist_ReturnsNull()
    {
        var service = new BatchAccessService(
            new FakeBatchRepository(batch: null),
            new FakeRunRepository());

        var result =
            await service.GetOwnedBatchAsync(Guid.NewGuid(), OwnerUserId);

        Assert.That(result, Is.Null);
    }

    // --------------------------------------------------------- GetOwnedRunAsync

    [Test]
    public async Task GetOwnedRunAsync_WhenTheCallerOwnsTheRunsBatch_ReturnsTheRun()
    {
        var batch = OwnedBatch();
        var run = new ReconciliationRun(batch.Id);

        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository(run));

        var result =
            await service.GetOwnedRunAsync(run.Id, OwnerUserId);

        Assert.That(result, Is.SameAs(run));
    }

    [Test]
    public async Task GetOwnedRunAsync_WhenTheCallerDoesNotOwnTheRunsBatch_ReturnsNull()
    {
        var batch = OwnedBatch();
        var run = new ReconciliationRun(batch.Id);

        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository(run));

        var result =
            await service.GetOwnedRunAsync(run.Id, OtherUserId);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetOwnedRunAsync_WhenTheRunDoesNotExist_ReturnsNull()
    {
        var service = new BatchAccessService(
            new FakeBatchRepository(batch: null),
            new FakeRunRepository(run: null));

        var result =
            await service.GetOwnedRunAsync(Guid.NewGuid(), OwnerUserId);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetOwnedRunAsync_WhenTheRunsBatchIsUnowned_ReturnsNullForEveryCaller()
    {
        var batch = UnownedLegacyBatch();
        var run = new ReconciliationRun(batch.Id);

        var service = new BatchAccessService(
            new FakeBatchRepository(batch),
            new FakeRunRepository(run));

        Assert.That(
            await service.GetOwnedRunAsync(run.Id, OwnerUserId),
            Is.Null);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeBatchRepository : IBatchRepository
    {
        private readonly Batch? _batch;

        public FakeBatchRepository(Batch? batch) => _batch = batch;

        public Task<Batch?> GetByIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _batch is not null && _batch.Id == batchId ? _batch : null);

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageByOwnerAsync(
            Guid ownerUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(
            Batch batch,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRunRepository : IReconciliationRunRepository
    {
        private readonly ReconciliationRun? _run;

        public FakeRunRepository(ReconciliationRun? run = null) => _run = run;

        public Task<ReconciliationRun?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _run is not null && _run.Id == id ? _run : null);

        public Task AddAsync(
            ReconciliationRun run,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
