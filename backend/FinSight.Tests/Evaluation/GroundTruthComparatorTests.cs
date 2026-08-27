using FinSight.DataGenerator.Models;
using FinSight.DataGenerator.Validation;

namespace FinSight.Tests.Evaluation;

/// <summary>
/// Unit-level tests for GroundTruthComparator. No database, no HTTP --
/// Compare() is a pure function over in-memory lists, and
/// AccumulatePagesAsync is tested with a plain in-memory delegate.
///
/// These prove the comparator's discrepancy-detection surface directly,
/// which had zero automated coverage before Phase 2 (FinSight.Tests had
/// no project reference to FinSight.DataGenerator).
/// </summary>
[TestFixture]
public sealed class GroundTruthComparatorTests
{
    // Fixture: 3 expected transactions covering Matched (no exception),
    // Mismatched (AmountMismatch exception), Missing (MissingRecord
    // exception) -- a small but representative slice of every
    // comparison dimension the comparator checks.
    private static List<GroundTruthRow> BuildExpectedRows()
    {
        return new List<GroundTruthRow>
        {
            new(
                "TXN-1", "ExactMatch", "Matched", "EXACT_MATCH",
                "", true, true, true, "Exact", "Exact"),

            new(
                "TXN-2", "AmountMismatch", "Mismatched", "AMOUNT_MISMATCH",
                "AmountMismatch", true, true, true,
                "BankAndSettlementMinus10", "Exact"),

            new(
                "TXN-3", "MissingPayment", "Missing", "SOURCE_ABSENT_PAYMENT",
                "MissingRecord", false, true, true,
                "NotComparable", "NotComparable")
        };
    }

    private static List<GroundTruthComparator.ActualResult> BuildMatchingActualResults()
    {
        return new List<GroundTruthComparator.ActualResult>
        {
            new()
            {
                ResultId = Guid.NewGuid(),
                TransactionReference = "TXN-1",
                Status = "Matched",
                ReasonCode = "EXACT_MATCH"
            },
            new()
            {
                ResultId = Guid.NewGuid(),
                TransactionReference = "TXN-2",
                Status = "Mismatched",
                ReasonCode = "AMOUNT_MISMATCH"
            },
            new()
            {
                ResultId = Guid.NewGuid(),
                TransactionReference = "TXN-3",
                Status = "Missing",
                ReasonCode = "SOURCE_ABSENT_PAYMENT"
            }
        };
    }

    private static List<GroundTruthComparator.ActualException> BuildMatchingActualExceptions()
    {
        return new List<GroundTruthComparator.ActualException>
        {
            new()
            {
                ExceptionId = Guid.NewGuid(),
                TransactionReference = "TXN-2",
                Category = "AmountMismatch"
            },
            new()
            {
                ExceptionId = Guid.NewGuid(),
                TransactionReference = "TXN-3",
                Category = "MissingRecord"
            }
        };
    }

    [Test]
    public void Compare_AllMatching_ReturnsSuccessWithNoFailures()
    {
        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            BuildMatchingActualResults(),
            BuildMatchingActualExceptions());

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Failures, Is.Empty);
        Assert.That(result.ExpectedTotalUnits, Is.EqualTo(3));
        Assert.That(result.ActualTotalUnits, Is.EqualTo(3));
        Assert.That(result.ExpectedMatched, Is.EqualTo(1));
        Assert.That(result.ActualMatched, Is.EqualTo(1));
        Assert.That(result.ExpectedMismatched, Is.EqualTo(1));
        Assert.That(result.ExpectedMissing, Is.EqualTo(1));
    }

    [Test]
    public void Compare_StatusMismatch_RecordsTransactionAndBucketFailures()
    {
        var actualResults = BuildMatchingActualResults();

        // Corrupt TXN-2: report it as Matched instead of Mismatched.
        actualResults[1] = new GroundTruthComparator.ActualResult
        {
            ResultId = actualResults[1].ResultId,
            TransactionReference = "TXN-2",
            Status = "Matched",
            ReasonCode = "AMOUNT_MISMATCH"
        };

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            actualResults,
            BuildMatchingActualExceptions());

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains("TXN-2: status mismatch"));

        // A single status mismatch shifts both aggregate buckets --
        // proving the evaluator does not stop at the first failure it
        // finds, it reports the full picture.
        Assert.That(
            result.Failures,
            Has.Some.Contains("Matched count mismatch"));

        Assert.That(
            result.Failures,
            Has.Some.Contains("Mismatched count mismatch"));
    }

    [Test]
    public void Compare_ReasonCodeMismatch_RecordsFailureWithoutStatusMismatch()
    {
        var actualResults = BuildMatchingActualResults();

        actualResults[1] = new GroundTruthComparator.ActualResult
        {
            ResultId = actualResults[1].ResultId,
            TransactionReference = "TXN-2",
            Status = "Mismatched",
            ReasonCode = "DATE_OUT_OF_TOLERANCE"
        };

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            actualResults,
            BuildMatchingActualExceptions());

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains("TXN-2: reason-code mismatch"));

        Assert.That(
            result.Failures,
            Has.None.Contains("TXN-2: status mismatch"));
    }

    [Test]
    public void Compare_MissingExpectedRecord_RecordsFailure()
    {
        var actualResults =
            BuildMatchingActualResults()
                .Where(x => x.TransactionReference != "TXN-3")
                .ToList();

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            actualResults,
            BuildMatchingActualExceptions());

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains(
                "TXN-3: missing from reconciliation results"));
    }

    [Test]
    public void Compare_UnexpectedActualRecord_RecordsFailure()
    {
        var actualResults = BuildMatchingActualResults();

        actualResults.Add(
            new GroundTruthComparator.ActualResult
            {
                ResultId = Guid.NewGuid(),
                TransactionReference = "TXN-4",
                Status = "Matched",
                ReasonCode = "EXACT_MATCH"
            });

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            actualResults,
            BuildMatchingActualExceptions());

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains(
                "TXN-4: returned by reconciliation but missing from ground truth"));
    }

    [Test]
    public void Compare_MissingExpectedException_RecordsFailure()
    {
        var actualExceptions =
            BuildMatchingActualExceptions()
                .Where(x => x.TransactionReference != "TXN-2")
                .ToList();

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            BuildMatchingActualResults(),
            actualExceptions);

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains(
                "TXN-2: expected exception 'AmountMismatch' but no exception was returned"));

        Assert.That(
            result.Failures,
            Has.Some.Contains("Exception category 'AmountMismatch' mismatch"));
    }

    [Test]
    public void Compare_UnexpectedActualException_RecordsFailure()
    {
        var actualExceptions = BuildMatchingActualExceptions();

        actualExceptions.Add(
            new GroundTruthComparator.ActualException
            {
                ExceptionId = Guid.NewGuid(),
                TransactionReference = "TXN-1",
                Category = "Unresolved"
            });

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            BuildMatchingActualResults(),
            actualExceptions);

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains("TXN-1: unexpected exception returned"));

        Assert.That(
            result.Failures,
            Has.Some.Contains("TXN-1: unexpected exception reference"));
    }

    [Test]
    public void Compare_ExceptionCategoryMismatch_RecordsFailure()
    {
        var actualExceptions = BuildMatchingActualExceptions();

        actualExceptions[0] = new GroundTruthComparator.ActualException
        {
            ExceptionId = actualExceptions[0].ExceptionId,
            TransactionReference = "TXN-2",
            Category = "DateMismatch"
        };

        var result = new GroundTruthComparator().Compare(
            BuildExpectedRows(),
            BuildMatchingActualResults(),
            actualExceptions);

        Assert.That(result.IsSuccess, Is.False);

        Assert.That(
            result.Failures,
            Has.Some.Contains("TXN-2: exception category mismatch"));
    }

    [Test]
    public void Compare_CalledTwiceWithSameFailingInput_ProducesIdenticalFailureListInSameOrder()
    {
        var expectedRows = BuildExpectedRows();

        var actualResults = BuildMatchingActualResults();

        actualResults[0] = new GroundTruthComparator.ActualResult
        {
            ResultId = actualResults[0].ResultId,
            TransactionReference = "TXN-1",
            Status = "Mismatched",
            ReasonCode = "EXACT_MATCH"
        };

        actualResults.Add(
            new GroundTruthComparator.ActualResult
            {
                ResultId = Guid.NewGuid(),
                TransactionReference = "TXN-99",
                Status = "Matched",
                ReasonCode = "EXACT_MATCH"
            });

        var actualExceptions = BuildMatchingActualExceptions();

        var comparator = new GroundTruthComparator();

        var first = comparator.Compare(
            expectedRows,
            actualResults,
            actualExceptions);

        var second = comparator.Compare(
            expectedRows,
            actualResults,
            actualExceptions);

        Assert.That(first.IsSuccess, Is.False);
        Assert.That(first.Failures, Is.Not.Empty);
        Assert.That(second.Failures, Is.EqualTo(first.Failures));
    }

    // ------------------------------------------------------------
    // AccumulatePagesAsync -- the pagination loop that was entirely
    // absent before Phase 2 (CompareAsync used to fetch only page 1).
    // ------------------------------------------------------------

    [Test]
    public async Task AccumulatePagesAsync_SinglePage_ReturnsAllItemsFromOneCall()
    {
        var callCount = 0;

        var items = await GroundTruthComparator.AccumulatePagesAsync<int>(
            pageNumber =>
            {
                callCount++;

                return Task.FromResult(
                    new GroundTruthComparator.PagedEnvelope<int>
                    {
                        Items = new List<int> { 1, 2, 3 },
                        TotalPages = 1
                    });
            });

        Assert.That(items, Is.EqualTo(new List<int> { 1, 2, 3 }));
        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public async Task AccumulatePagesAsync_MultiplePages_AccumulatesItemsAcrossAllPages()
    {
        var callCount = 0;

        var items = await GroundTruthComparator.AccumulatePagesAsync<int>(
            pageNumber =>
            {
                callCount++;

                return Task.FromResult(
                    pageNumber switch
                    {
                        1 => new GroundTruthComparator.PagedEnvelope<int>
                        {
                            Items = new List<int> { 1, 2 },
                            TotalPages = 3
                        },
                        2 => new GroundTruthComparator.PagedEnvelope<int>
                        {
                            Items = new List<int> { 3, 4 },
                            TotalPages = 3
                        },
                        3 => new GroundTruthComparator.PagedEnvelope<int>
                        {
                            Items = new List<int> { 5 },
                            TotalPages = 3
                        },
                        _ => throw new InvalidOperationException(
                            $"Unexpected page {pageNumber} requested.")
                    });
            });

        Assert.That(items, Is.EqualTo(new List<int> { 1, 2, 3, 4, 5 }));
        Assert.That(callCount, Is.EqualTo(3));
    }

    [Test]
    public async Task AccumulatePagesAsync_EmptyIntermediatePage_StopsWithoutRequestingFurtherPages()
    {
        var callCount = 0;

        var items = await GroundTruthComparator.AccumulatePagesAsync<int>(
            pageNumber =>
            {
                callCount++;

                return Task.FromResult(
                    pageNumber switch
                    {
                        1 => new GroundTruthComparator.PagedEnvelope<int>
                        {
                            Items = new List<int> { 1, 2 },
                            TotalPages = 3
                        },
                        2 => new GroundTruthComparator.PagedEnvelope<int>
                        {
                            Items = new List<int>(),
                            TotalPages = 3
                        },
                        _ => throw new InvalidOperationException(
                            $"Page {pageNumber} must not be requested " +
                            "after an empty page was returned.")
                    });
            });

        Assert.That(items, Is.EqualTo(new List<int> { 1, 2 }));
        Assert.That(callCount, Is.EqualTo(2));
    }
}
