using FinSight.Api.Controllers;
using FinSight.Application.Abstractions.Evaluation;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Application.DTOs.Reconciliation;
using FinSight.Application.Evaluation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Tests.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

/// <summary>
/// Ownership enforcement across every run/exception-scoped action on
/// ReconciliationController.
///
/// The distinguishing assertion in most of these tests is not just the
/// 404 status -- it's that the underlying dependency (summary builder,
/// ground-truth comparer, result/exception repositories, AI explanation
/// service) is never invoked when ownership fails. A 404 that still ran
/// the real comparison or the real AI call first would be a data leak
/// wearing a correct status code.
/// </summary>
[TestFixture]
public sealed class ReconciliationControllerOwnershipTests
{
    private static readonly Guid CurrentUserId = Guid.NewGuid();
    private static readonly Guid OwnedRunId = Guid.NewGuid();
    private static readonly Guid NotOwnedRunId = Guid.NewGuid();

    private FakeBatchAccessService _accessService = null!;
    private FakeReconciliationService _reconciliationService = null!;
    private FakeResultRepository _resultRepository = null!;
    private FakeExceptionRepository _exceptionRepository = null!;
    private FakeNormalizedTransactionRepository _normalizedTransactionRepository = null!;
    private FakeSummaryBuilder _summaryBuilder = null!;
    private FakeGroundTruthService _groundTruthService = null!;
    private FakeAiExplanationService _aiExplanationService = null!;
    private FakeAuditLogReader _auditLogReader = null!;
    private ReconciliationController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _accessService = new FakeBatchAccessService();
        _accessService.OwnRun(OwnedRunId, CurrentUserId);

        _reconciliationService = new FakeReconciliationService();
        _resultRepository = new FakeResultRepository();
        _exceptionRepository = new FakeExceptionRepository();
        _normalizedTransactionRepository = new FakeNormalizedTransactionRepository();
        _summaryBuilder = new FakeSummaryBuilder();
        _groundTruthService = new FakeGroundTruthService();
        _aiExplanationService = new FakeAiExplanationService();
        _auditLogReader = new FakeAuditLogReader();

        _controller = new ReconciliationController(
            _reconciliationService,
            new FakeRunRepository(),
            _resultRepository,
            _exceptionRepository,
            _normalizedTransactionRepository,
            new ThrowingPaymentRecordRepository(),
            new ThrowingBankRecordRepository(),
            new ThrowingSettlementRecordRepository(),
            _aiExplanationService,
            _summaryBuilder,
            _groundTruthService,
            new FixedCurrentUserService(CurrentUserId),
            _accessService,
            _auditLogReader);
    }

    private static void AssertNotFound(ActionResult? result)
    {
        var objectResult = result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);
        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound));
    }

    // ------------------------------------------------------------- CreateRun

    [Test]
    public async Task CreateRun_WhenTheBatchDoesNotBelongToTheCaller_Returns404WithoutRunningReconciliation()
    {
        var result =
            await _controller.CreateRun(
                new ReconciliationRunRequest { BatchId = Guid.NewGuid() },
                CancellationToken.None);

        AssertNotFound(result.Result);

        Assert.That(
            _reconciliationService.Calls,
            Is.Zero,
            "The reconciliation engine must never run against a batch " +
            "the caller does not own.");
    }

    // ----------------------------------------------------------------- GetRun

    [Test]
    public async Task GetRun_WhenTheRunIsNotOwnedByTheCaller_Returns404()
    {
        var result =
            await _controller.GetRun(NotOwnedRunId, CancellationToken.None);

        AssertNotFound(result.Result);
    }

    // ------------------------------------------------------------- GetSummary

    [Test]
    public async Task GetSummary_WhenTheRunIsNotOwnedByTheCaller_Returns404WithoutBuildingIt()
    {
        var result =
            await _controller.GetSummary(NotOwnedRunId, CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_summaryBuilder.Calls, Is.Zero);
    }

    [Test]
    public async Task GetSummary_WhenTheCallerOwnsTheRun_BuildsIt()
    {
        var result =
            await _controller.GetSummary(OwnedRunId, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_summaryBuilder.Calls, Is.EqualTo(1));
    }

    // ------------------------------------------------------ VerifyGroundTruth

    [Test]
    public async Task VerifyGroundTruth_WhenTheRunIsNotOwnedByTheCaller_Returns404WithoutComparing()
    {
        var rows = new[]
        {
            new GroundTruthRow(
                "TXN-0001", "ExactMatch", "Matched", "EXACT_MATCH", "",
                true, true, true, "Exact", "Exact"),
        };

        var result =
            await _controller.VerifyGroundTruth(
                NotOwnedRunId, rows, CancellationToken.None);

        AssertNotFound(result.Result);

        // The single highest-value assertion in this whole file: an
        // unauthorized caller must not be able to trigger the ground-
        // truth comparison at all, regardless of what labels they supply.
        Assert.That(_groundTruthService.Calls, Is.Zero);
    }

    // -------------------------------------------------------------- GetResults

    [Test]
    public async Task GetResults_WhenTheRunIsNotOwnedByTheCaller_Returns404WithoutQueryingResults()
    {
        var result =
            await _controller.GetResults(
                NotOwnedRunId, 1, 50, CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_resultRepository.PageCalls, Is.Zero);
    }

    // ----------------------------------------------------------- GetExceptions

    [Test]
    public async Task GetExceptions_WhenTheRunIsNotOwnedByTheCaller_Returns404WithoutQueryingExceptions()
    {
        var result =
            await _controller.GetExceptions(
                NotOwnedRunId, 1, 50, CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_exceptionRepository.PageCalls, Is.Zero);
    }

    // ------------------------------------------------------------- GetAuditLog

    [Test]
    public async Task GetAuditLog_WhenTheRunIsNotOwnedByTheCaller_Returns404WithoutQueryingAudit()
    {
        var result =
            await _controller.GetAuditLog(
                NotOwnedRunId, 1, 50, CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_auditLogReader.Calls, Is.Zero);
    }

    [Test]
    public async Task GetAuditLog_WhenTheRunDoesNotExist_Returns404()
    {
        var result =
            await _controller.GetAuditLog(
                Guid.NewGuid(), 1, 50, CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_auditLogReader.Calls, Is.Zero);
    }

    [Test]
    public async Task GetAuditLog_WhenTheCallerOwnsTheRun_ReturnsItsAuditEvidence()
    {
        var auditLog =
            new AuditLog(
                AuditEventType.ReconciliationCompleted,
                $$"""{"run_id":"{{OwnedRunId}}","duration_ms":42}""",
                OwnedRunId);

        _auditLogReader.Seed(auditLog);

        var result =
            await _controller.GetAuditLog(
                OwnedRunId, 1, 50, CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        var page = okResult!.Value as PagedResponse<AuditLogEntryResponse>;

        Assert.Multiple(() =>
        {
            Assert.That(okResult, Is.Not.Null);
            Assert.That(page!.Items, Has.Count.EqualTo(1));
            Assert.That(page.Items[0].Id, Is.EqualTo(auditLog.Id));
            Assert.That(page.Items[0].EventType, Is.EqualTo("ReconciliationCompleted"));
            Assert.That(page.Items[0].RunId, Is.EqualTo(OwnedRunId));
        });
    }

    [Test]
    public async Task GetAuditLog_WhenNoEventsExistForTheRun_ReturnsAnEmptyPageNotAnError()
    {
        var result =
            await _controller.GetAuditLog(
                OwnedRunId, 1, 50, CancellationToken.None);

        var okResult = result.Result as OkObjectResult;
        var page = okResult!.Value as PagedResponse<AuditLogEntryResponse>;

        Assert.Multiple(() =>
        {
            Assert.That(okResult, Is.Not.Null);
            Assert.That(page!.Items, Is.Empty);
            Assert.That(page.TotalCount, Is.Zero);
        });
    }

    [Test]
    public async Task GetAuditLog_WithAnInvalidPageNumber_Returns400WithoutQueryingAudit()
    {
        var result =
            await _controller.GetAuditLog(
                OwnedRunId, 0, 50, CancellationToken.None);

        var objectResult = result.Result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);
        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest));
        Assert.That(_auditLogReader.Calls, Is.Zero);
    }

    // ------------------------------------------------------------ GetException

    [Test]
    public async Task GetException_WhenTheExceptionsRunIsNotOwnedByTheCaller_Returns404()
    {
        // The IDOR case: the route carries no runId at all, and the
        // exception genuinely exists -- only the ownership check on its
        // parent run stands between the caller and the data.
        var exception = SampleException(NotOwnedRunId);
        _exceptionRepository.Seed(exception);

        var result =
            await _controller.GetException(
                exception.Id, CancellationToken.None);

        AssertNotFound(result.Result);
    }

    [Test]
    public async Task GetException_WhenTheCallerOwnsTheExceptionsRun_ReturnsIt()
    {
        var normalizedTransaction =
            new NormalizedTransaction(
                OwnedRunId, "TXN-0001", Guid.NewGuid(), null, null);

        var result =
            new ReconciliationResult(
                OwnedRunId,
                normalizedTransaction.Id,
                MatchStatus.Missing,
                ReconciliationReasonCode.SOURCE_ABSENT_BANK);

        var exception =
            new ReconciliationException(
                OwnedRunId, result.Id, ExceptionCategory.MissingRecord,
                "Payment", "{}");

        _exceptionRepository.Seed(exception);
        _resultRepository.Seed(result);
        _normalizedTransactionRepository.Seed(normalizedTransaction);

        var actionResult =
            await _controller.GetException(exception.Id, CancellationToken.None);

        Assert.That(actionResult.Result, Is.InstanceOf<OkObjectResult>());
    }

    // ------------------------------------------------------ GenerateAiExplanation

    [Test]
    public async Task GenerateAiExplanation_WhenTheExceptionsRunIsNotOwnedByTheCaller_Returns404WithoutCallingAi()
    {
        var exception = SampleException(NotOwnedRunId);
        _exceptionRepository.Seed(exception);

        var result =
            await _controller.GenerateAiExplanation(
                exception.Id, CancellationToken.None);

        AssertNotFound(result.Result);

        Assert.That(
            _aiExplanationService.Calls,
            Is.Zero,
            "AI must never be invoked for an exception belonging to a " +
            "run the caller does not own.");
    }

    [Test]
    public async Task GenerateAiExplanation_WhenTheExceptionDoesNotExist_FallsThroughToTheServicesOwnNotFoundHandling()
    {
        // No seeded exception at all -- ownership cannot be checked
        // (there is no run to check), so this must still reach the real
        // service and get its normal 404, unaffected by this phase.
        _aiExplanationService.ThrowKeyNotFound = true;

        var result =
            await _controller.GenerateAiExplanation(
                Guid.NewGuid(), CancellationToken.None);

        AssertNotFound(result.Result);
        Assert.That(_aiExplanationService.Calls, Is.EqualTo(1));
    }

    private static ReconciliationException SampleException(Guid runId) =>
        new(
            runId,
            Guid.NewGuid(),
            ExceptionCategory.MissingRecord,
            "Payment",
            "{}");


    // ------------------------------------------------------------------ fakes

    private sealed class FakeBatchAccessService : IBatchAccessService
    {
        private readonly Dictionary<Guid, Guid> _runOwners = new();

        public void OwnRun(Guid runId, Guid ownerId) => _runOwners[runId] = ownerId;

        public Task<Batch?> GetOwnedBatchAsync(
            Guid batchId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Batch?>(null);

        public Task<ReconciliationRun?> GetOwnedRunAsync(
            Guid runId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (_runOwners.TryGetValue(runId, out var owner) && owner == userId)
            {
                return Task.FromResult<ReconciliationRun?>(
                    new ReconciliationRun(Guid.NewGuid()));
            }

            return Task.FromResult<ReconciliationRun?>(null);
        }
    }

    private sealed class FakeReconciliationService : IReconciliationService
    {
        public int Calls { get; private set; }

        public Task<ReconciliationRunResult> ExecuteAsync(
            ReconciliationRunRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException(
                "Not expected to be reached in these tests.");
        }
    }

    private sealed class FakeRunRepository : IReconciliationRunRepository
    {
        public Task<ReconciliationRun?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Ownership resolution goes through IBatchAccessService.");

        public Task AddAsync(
            ReconciliationRun run,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeResultRepository : IReconciliationResultRepository
    {
        private readonly List<ReconciliationResult> _results = [];

        public int PageCalls { get; private set; }

        public void Seed(ReconciliationResult result) => _results.Add(result);

        public Task<ReconciliationResult?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<ReconciliationResult>> GetByRunIdAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReconciliationResult>>(
                _results.Where(x => x.RunId == runId).ToList());

        public Task<(IReadOnlyList<ReconciliationResult> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            PageCalls++;
            return Task.FromResult(
                ((IReadOnlyList<ReconciliationResult>)Array.Empty<ReconciliationResult>(), 0));
        }

        public Task AddAsync(
            ReconciliationResult result,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationResult> results,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeExceptionRepository : IReconciliationExceptionRepository
    {
        private readonly List<ReconciliationException> _exceptions = [];

        public int PageCalls { get; private set; }

        public void Seed(ReconciliationException exception) =>
            _exceptions.Add(exception);

        public Task<ReconciliationException?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_exceptions.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<ReconciliationException>> GetByRunIdAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReconciliationException>>(
                _exceptions.Where(x => x.RunId == runId).ToList());

        public Task<(IReadOnlyList<ReconciliationException> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
        {
            PageCalls++;
            return Task.FromResult(
                ((IReadOnlyList<ReconciliationException>)Array.Empty<ReconciliationException>(), 0));
        }

        public Task AddAsync(
            ReconciliationException exception,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationException> exceptions,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAuditLogReader : IAuditLogReader
    {
        private readonly List<AuditLog> _auditLogs = [];

        public int Calls { get; private set; }

        public void Seed(AuditLog auditLog) => _auditLogs.Add(auditLog);

        public Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPageByRunIdAsync(
            Guid runId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            var matching =
                _auditLogs
                    .Where(x => x.RunId == runId)
                    .OrderByDescending(x => x.OccurredAt)
                    .ThenByDescending(x => x.Id)
                    .ToList();

            return Task.FromResult(
                ((IReadOnlyList<AuditLog>)matching, matching.Count));
        }
    }

    private sealed class FakeSummaryBuilder : IReconciliationSummaryBuilder
    {
        public int Calls { get; private set; }

        public Task<ReconciliationRunSummaryResponse?> BuildAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult<ReconciliationRunSummaryResponse?>(
                new ReconciliationRunSummaryResponse { RunId = runId });
        }
    }

    private sealed class FakeGroundTruthService : IGroundTruthComparisonService
    {
        public int Calls { get; private set; }

        public Task<GroundTruthComparisonResult> CompareAsync(
            Guid runId,
            IReadOnlyList<GroundTruthRow> groundTruthRows,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException(
                "Not expected to be reached in these tests.");
        }
    }

    private sealed class FakeAiExplanationService : IAiExplanationService
    {
        public int Calls { get; private set; }

        public bool ThrowKeyNotFound { get; set; }

        public Task<AiExplanationResponse> ExplainAsync(
            Guid exceptionId,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (ThrowKeyNotFound)
            {
                throw new KeyNotFoundException(
                    $"Reconciliation exception '{exceptionId}' was not found.");
            }

            throw new InvalidOperationException(
                "Not expected to be reached in these tests.");
        }
    }

    private sealed class FakeNormalizedTransactionRepository
        : INormalizedTransactionRepository
    {
        private readonly List<NormalizedTransaction> _transactions = [];

        public void Seed(NormalizedTransaction transaction) =>
            _transactions.Add(transaction);

        public Task<NormalizedTransaction?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_transactions.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<NormalizedTransaction>> GetByRunIdAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NormalizedTransaction>>(
                _transactions.Where(x => x.RunId == runId).ToList());

        public Task AddAsync(
            NormalizedTransaction transaction,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<NormalizedTransaction> transactions,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingPaymentRecordRepository : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PaymentRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<PaymentRecord> records,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingBankRecordRepository : IBankRecordRepository
    {
        public Task<BankRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BankRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<BankRecord> records,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingSettlementRecordRepository
        : ISettlementRecordRepository
    {
        public Task<SettlementRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SettlementRecord>> GetByBatchIdAsync(
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(
            IReadOnlyCollection<SettlementRecord> records,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
