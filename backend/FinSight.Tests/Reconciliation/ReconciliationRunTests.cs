using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Tests.Reconciliation;

public class ReconciliationRunTests
{
    [Test]
    public void Constructor_WithValidBatchId_CreatesPendingRun()
    {
        var batchId = Guid.NewGuid();

        var run =
            new ReconciliationRun(batchId);

        Assert.Multiple(() =>
        {
            Assert.That(
                run.BatchId,
                Is.EqualTo(batchId));

            Assert.That(
                run.Status,
                Is.EqualTo(
                    ReconciliationRunStatus.Pending));

            Assert.That(
                run.TotalReconciliationUnits,
                Is.EqualTo(0));

            Assert.That(
                run.MatchRate,
                Is.Null);

            Assert.That(
                run.CompletedAt,
                Is.Null);

            Assert.That(
                run.Id,
                Is.Not.EqualTo(Guid.Empty));
        });
    }

    [Test]
    public void Constructor_WithEmptyBatchId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new ReconciliationRun(Guid.Empty));
    }

    [Test]
    public void MarkRunning_FromPending_TransitionsToRunning()
    {
        var run =
            new ReconciliationRun(
                Guid.NewGuid());

        run.MarkRunning();

        Assert.That(
            run.Status,
            Is.EqualTo(
                ReconciliationRunStatus.Running));
    }

    [Test]
    public void Complete_FromRunning_TransitionsToCompleted()
    {
        var run =
            CreateRunningRun();

        run.Complete(
            totalReconciliationUnits: 100,
            matchRate: 87.3456m);

        Assert.Multiple(() =>
        {
            Assert.That(
                run.Status,
                Is.EqualTo(
                    ReconciliationRunStatus.Completed));

            Assert.That(
                run.TotalReconciliationUnits,
                Is.EqualTo(100));

            Assert.That(
                run.MatchRate,
                Is.EqualTo(87.35m));

            Assert.That(
                run.CompletedAt,
                Is.Not.Null);
        });
    }

    [Test]
    public void Fail_FromRunning_TransitionsToFailed()
    {
        var run =
            CreateRunningRun();

        run.Fail();

        Assert.Multiple(() =>
        {
            Assert.That(
                run.Status,
                Is.EqualTo(
                    ReconciliationRunStatus.Failed));

            Assert.That(
                run.CompletedAt,
                Is.Not.Null);
        });
    }

    [Test]
    public void MarkRunning_FromRunning_Throws()
    {
        var run =
            CreateRunningRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.MarkRunning());
    }

    [Test]
    public void Complete_FromPending_Throws()
    {
        var run =
            new ReconciliationRun(
                Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Complete(
                    10,
                    50m));
    }

    [Test]
    public void Fail_FromPending_Throws()
    {
        var run =
            new ReconciliationRun(
                Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Fail());
    }

    [Test]
    public void Complete_FromCompleted_Throws()
    {
        var run =
            CreateCompletedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Complete(
                    20,
                    80m));
    }

    [Test]
    public void MarkRunning_FromCompleted_Throws()
    {
        var run =
            CreateCompletedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.MarkRunning());
    }

    [Test]
    public void Fail_FromCompleted_Throws()
    {
        var run =
            CreateCompletedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Fail());
    }

    [Test]
    public void MarkRunning_FromFailed_Throws()
    {
        var run =
            CreateFailedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.MarkRunning());
    }

    [Test]
    public void Complete_FromFailed_Throws()
    {
        var run =
            CreateFailedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Complete(
                    20,
                    80m));
    }

    [Test]
    public void Fail_FromFailed_Throws()
    {
        var run =
            CreateFailedRun();

        Assert.Throws<InvalidOperationException>(
            () =>
                run.Fail());
    }

    [Test]
    public void Complete_WithNegativeUnits_Throws()
    {
        var run =
            CreateRunningRun();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                run.Complete(
                    -1,
                    50m));
    }

    [Test]
    public void Complete_WithMatchRateBelowZero_Throws()
    {
        var run =
            CreateRunningRun();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                run.Complete(
                    10,
                    -0.01m));
    }

    [Test]
    public void Complete_WithMatchRateAbove100_Throws()
    {
        var run =
            CreateRunningRun();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                run.Complete(
                    10,
                    100.01m));
    }

    private static ReconciliationRun CreateRunningRun()
    {
        var run =
            new ReconciliationRun(
                Guid.NewGuid());

        run.MarkRunning();

        return run;
    }

    private static ReconciliationRun CreateCompletedRun()
    {
        var run =
            CreateRunningRun();

        run.Complete(
            10,
            75m);

        return run;
    }

    private static ReconciliationRun CreateFailedRun()
    {
        var run =
            CreateRunningRun();

        run.Fail();

        return run;
    }
}