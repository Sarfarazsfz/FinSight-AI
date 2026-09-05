using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Reconciliation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Tests.Reconciliation;

/// <summary>
/// Throughput reporting on the run summary.
///
/// The figures are derived from the run's own persisted StartedAt /
/// CompletedAt, so these run against faked repositories with no database:
/// the behaviour under test is the arithmetic and the absent-value
/// handling, not persistence.
/// </summary>
[TestFixture]
public sealed class ReconciliationSummaryThroughputTests
{
    private static readonly Guid RunId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid BatchId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// Builds a run whose StartedAt/CompletedAt differ by exactly
    /// <paramref name="durationMs"/>. The entity stamps both from
    /// DateTime.UtcNow, so the values are rewritten via reflection --
    /// the alternative would be injecting a clock into the domain purely
    /// to satisfy a test.
    /// </summary>
    private static ReconciliationRun CompletedRun(
        int totalUnits,
        double durationMs)
    {
        var run = new ReconciliationRun(BatchId);
        run.MarkRunning();
        run.Complete(totalUnits, 100m);

        var startedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        Set(run, nameof(ReconciliationRun.StartedAt), startedAt);
        Set(
            run,
            nameof(ReconciliationRun.CompletedAt),
            startedAt.AddMilliseconds(durationMs));

        return run;
    }

    private static ReconciliationRun RunningRun()
    {
        var run = new ReconciliationRun(BatchId);
        run.MarkRunning();
        return run;
    }

    private static void Set(object target, string property, object? value) =>
        target
            .GetType()
            .GetProperty(property)!
            .SetValue(target, value);

    private static ReconciliationResult MatchedResult() =>
        new(
            RunId,
            Guid.NewGuid(),
            MatchStatus.Matched,
            ReconciliationReasonCode.EXACT_MATCH,
            "StrategyOne_ExactReferenceMatch");

    private static async Task<Application.DTOs.Reconciliation.ReconciliationRunSummaryResponse?>
        BuildAsync(ReconciliationRun run, int resultCount)
    {
        var results =
            Enumerable.Range(0, resultCount).Select(_ => MatchedResult()).ToList();

        var builder = new ReconciliationSummaryBuilder(
            new FakeRunRepository(run),
            new FakeResultRepository(results),
            new FakeExceptionRepository());

        return await builder.BuildAsync(run.Id);
    }

    [Test]
    public async Task Summary_ForACompletedRun_ReportsDurationFromThePersistedTimestamps()
    {
        var summary = await BuildAsync(CompletedRun(100, 50), 100);

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary!.DurationMs, Is.EqualTo(50).Within(0.001));
    }

    [Test]
    public async Task Summary_ForACompletedRun_ReportsUnitsPerSecond()
    {
        // 100 units in 50 ms => 2000 units/second.
        var summary = await BuildAsync(CompletedRun(100, 50), 100);

        Assert.That(summary!.RecordsPerSecond, Is.EqualTo(2000).Within(0.001));
    }

    [Test]
    public async Task Summary_ThroughputIsConsistentWithTheReportedUnitsAndDuration()
    {
        var summary = await BuildAsync(CompletedRun(250, 125), 250);

        // The rate must be exactly derivable from the other two reported
        // numbers -- a reviewer can check it by hand.
        var expected = summary!.TotalUnits / (summary.DurationMs!.Value / 1000d);

        Assert.That(summary.RecordsPerSecond, Is.EqualTo(expected).Within(0.001));
    }

    [Test]
    public async Task Summary_ForARunThatHasNotCompleted_ReportsNoThroughputRatherThanZero()
    {
        var summary = await BuildAsync(RunningRun(), 10);

        Assert.Multiple(() =>
        {
            // Absent, not invented: an unfinished run has no duration.
            Assert.That(summary!.DurationMs, Is.Null);
            Assert.That(summary.RecordsPerSecond, Is.Null);
        });
    }

    [Test]
    public async Task Summary_ForAZeroDurationRun_ReportsDurationButNoRate()
    {
        // A sub-tick run would divide by zero; the duration is still real.
        var summary = await BuildAsync(CompletedRun(5, 0), 5);

        Assert.Multiple(() =>
        {
            Assert.That(summary!.DurationMs, Is.EqualTo(0));
            Assert.That(summary.RecordsPerSecond, Is.Null);
        });
    }

    [Test]
    public async Task Summary_ForAnEmptyRun_ReportsZeroThroughputWithoutFailing()
    {
        var summary = await BuildAsync(CompletedRun(0, 10), 0);

        Assert.Multiple(() =>
        {
            Assert.That(summary!.TotalUnits, Is.Zero);
            Assert.That(summary.DurationMs, Is.EqualTo(10).Within(0.001));
            Assert.That(summary.RecordsPerSecond, Is.EqualTo(0));
        });
    }

    // ------------------------------------------------------------- fakes

    private sealed class FakeRunRepository : IReconciliationRunRepository
    {
        private readonly ReconciliationRun _run;

        public FakeRunRepository(ReconciliationRun run) => _run = run;

        public Task<ReconciliationRun?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReconciliationRun?>(_run.Id == id ? _run : null);

        public Task AddAsync(
            ReconciliationRun run,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeResultRepository : IReconciliationResultRepository
    {
        private readonly IReadOnlyList<ReconciliationResult> _results;

        public FakeResultRepository(IReadOnlyList<ReconciliationResult> results) =>
            _results = results;

        public Task<ReconciliationResult?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReconciliationResult?>(null);

        public Task<IReadOnlyList<ReconciliationResult>> GetByRunIdAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_results);

        public Task<(IReadOnlyList<ReconciliationResult> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default) =>
            Task.FromResult((_results, _results.Count));

        public Task AddAsync(
            ReconciliationResult result,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationResult> results,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeExceptionRepository : IReconciliationExceptionRepository
    {
        private readonly IReadOnlyList<ReconciliationException> _empty = [];

        public Task<ReconciliationException?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReconciliationException?>(null);

        public Task<IReadOnlyList<ReconciliationException>> GetByRunIdAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_empty);

        public Task<(IReadOnlyList<ReconciliationException> Items, int TotalCount)>
            GetPageByRunIdAsync(
                Guid runId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default) =>
            Task.FromResult((_empty, 0));

        public Task AddAsync(
            ReconciliationException exception,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddRangeAsync(
            IReadOnlyCollection<ReconciliationException> exceptions,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
