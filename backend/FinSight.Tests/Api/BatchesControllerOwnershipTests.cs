using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ingestion;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Tests.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

/// <summary>
/// BatchesController's ownership enforcement, in isolation from HTTP and
/// the database. [Authorize] itself is proven separately (see
/// BatchesAuthorizationTests, which needs a real pipeline); these tests
/// cover the business logic those requests reach once authenticated.
/// </summary>
[TestFixture]
public sealed class BatchesControllerOwnershipTests
{
    private static readonly Guid CurrentUserId = Guid.NewGuid();
    private static readonly Guid OtherUsersBatchId = Guid.NewGuid();

    private static BatchesController CreateController(
        FakeBatchIngestionService? ingestionService = null,
        FakeBatchRepository? batchRepository = null,
        FakeBatchAccessService? batchAccessService = null) =>
        new(
            ingestionService ?? new FakeBatchIngestionService(),
            batchRepository ?? new FakeBatchRepository(),
            new FixedCurrentUserService(CurrentUserId),
            batchAccessService ?? new FakeBatchAccessService());

    // ---------------------------------------------------------------- GetBatch

    [Test]
    public async Task GetBatch_WhenTheBatchBelongsToAnotherUser_Returns404()
    {
        var accessService = new FakeBatchAccessService();
        // Deliberately does not register OtherUsersBatchId as owned by
        // CurrentUserId, so GetOwnedBatchAsync returns null -- exactly
        // what "exists, but is not yours" looks like to this service.
        var controller = CreateController(batchAccessService: accessService);

        var result =
            await controller.GetBatch(OtherUsersBatchId, CancellationToken.None);

        var objectResult = result.Result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);
        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task GetBatch_WhenTheBatchDoesNotExist_Returns404WithTheSameMessageAsNotOwned()
    {
        var controller = CreateController();

        var missingResult =
            await controller.GetBatch(Guid.NewGuid(), CancellationToken.None);

        var notOwnedResult =
            await CreateController(batchAccessService: new FakeBatchAccessService())
                .GetBatch(OtherUsersBatchId, CancellationToken.None);

        var missingDetail =
            ((missingResult.Result as ObjectResult)!.Value as ProblemDetails)!
                .Detail;

        var notOwnedDetail =
            ((notOwnedResult.Result as ObjectResult)!.Value as ProblemDetails)!
                .Detail;

        // Same status, same message shape -- a caller cannot distinguish
        // "does not exist" from "exists, but is not yours".
        Assert.That(missingDetail, Does.Contain("was not found"));
        Assert.That(notOwnedDetail, Does.Contain("was not found"));
    }

    [Test]
    public async Task GetBatch_WhenTheCallerOwnsTheBatch_ReturnsIt()
    {
        var batch = new Batch(
            "Mine", 0, 0, 0, "Valid", "me@example.test",
            createdByUserId: CurrentUserId);

        var accessService = new FakeBatchAccessService();
        accessService.Own(batch, CurrentUserId);

        var controller = CreateController(batchAccessService: accessService);

        var result =
            await controller.GetBatch(batch.Id, CancellationToken.None);

        var okResult = result.Result as OkObjectResult;

        Assert.That(okResult, Is.Not.Null);
        Assert.That(
            (okResult!.Value as BatchResponse)!.BatchId,
            Is.EqualTo(batch.Id));
    }

    // -------------------------------------------------------------- GetBatches

    [Test]
    public async Task GetBatches_PassesTheAuthenticatedCallerIdToTheRepository_NeverAnythingElse()
    {
        var repository = new FakeBatchRepository();
        var controller = CreateController(batchRepository: repository);

        await controller.GetBatches(1, 50, CancellationToken.None);

        // There is no field in this request the client could use to ask
        // for someone else's batches -- proving the repository received
        // exactly the authenticated identity closes that off structurally.
        Assert.That(repository.LastOwnerUserIdRequested, Is.EqualTo(CurrentUserId));
    }

    // ------------------------------------------------------------- CreateBatch

    [Test]
    public async Task CreateBatch_AssignsOwnershipFromTheAuthenticatedCaller_NeverFromRequestInput()
    {
        var ingestionService = new FakeBatchIngestionService();
        var controller = CreateController(ingestionService: ingestionService);

        var payments = FormFile("payments.csv");
        var bank = FormFile("bank.csv");
        var settlements = FormFile("settlements.csv");

        // A spoofed label is deliberately supplied in the untrusted
        // createdBy form field, to prove it has no bearing on ownership.
        await controller.CreateBatch(
            "Batch",
            "attacker@example.test",
            payments,
            bank,
            settlements,
            CancellationToken.None);

        Assert.That(ingestionService.LastRequest, Is.Not.Null);
        Assert.That(
            ingestionService.LastRequest!.CreatedByUserId,
            Is.EqualTo(CurrentUserId));
    }

    private static FormFile FormFile(string fileName)
    {
        var bytes = "id\n1\n"u8.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeBatchAccessService : IBatchAccessService
    {
        private readonly Dictionary<Guid, (Batch Batch, Guid OwnerId)> _owned = new();

        public void Own(Batch batch, Guid ownerId) =>
            _owned[batch.Id] = (batch, ownerId);

        public Task<Batch?> GetOwnedBatchAsync(
            Guid batchId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (_owned.TryGetValue(batchId, out var entry) && entry.OwnerId == userId)
            {
                return Task.FromResult<Batch?>(entry.Batch);
            }

            return Task.FromResult<Batch?>(null);
        }

        public Task<ReconciliationRun?> GetOwnedRunAsync(
            Guid runId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "BatchesController never resolves run ownership.");
    }

    private sealed class FakeBatchRepository : IBatchRepository
    {
        public Guid? LastOwnerUserIdRequested { get; private set; }

        public Task<Batch?> GetByIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "GetBatch resolves ownership through IBatchAccessService, " +
                "not this repository directly.");

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageAsync(
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "The controller must call the owner-scoped overload.");

        public Task<(IReadOnlyList<Batch> Items, int TotalCount)> GetPageByOwnerAsync(
            Guid ownerUserId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastOwnerUserIdRequested = ownerUserId;

            return Task.FromResult(
                ((IReadOnlyList<Batch>)Array.Empty<Batch>(), 0));
        }

        public Task AddAsync(
            Batch batch,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeBatchIngestionService : IBatchIngestionService
    {
        public BatchIngestionRequest? LastRequest { get; private set; }

        public Task<BatchIngestionResult> IngestAsync(
            BatchIngestionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(
                new BatchIngestionResult
                {
                    BatchId = Guid.NewGuid(),
                    ValidationStatus = "Valid",
                    PaymentRecordCount = 1,
                    BankRecordCount = 1,
                    SettlementRecordCount = 1,
                    TotalRecordCount = 3,
                });
        }
    }
}
