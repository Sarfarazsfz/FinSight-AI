namespace FinSight.Application.Evaluation;

/// <summary>
/// Pure, stateless ground-truth comparison logic. Moved verbatim (Phase
/// 4A) from FinSight.DataGenerator.Validation.GroundTruthComparator.Compare
/// so both the offline console verifier (FinSight.DataGenerator) and the
/// live HTTP ground-truth-verification endpoint (FinSight.Api) share the
/// exact same, already-tested implementation -- no second comparison
/// implementation.
/// </summary>
public static class GroundTruthComparer
{
    public static GroundTruthComparisonResult Compare(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        IReadOnlyList<ActualException> actualExceptions)
    {
        var failures = new List<string>();

        ValidateExpectedReferences(
            expectedRows,
            failures);

        ValidateActualReferences(
            actualResults,
            failures);

        CompareTransactionLevelResults(
            expectedRows,
            actualResults,
            failures);

        CompareTransactionLevelExceptions(
            expectedRows,
            actualExceptions,
            failures);

        var expectedCounts =
            BuildExpectedStatusCounts(expectedRows);

        var actualCounts =
            BuildActualStatusCounts(actualResults);

        CompareStatusCount(
            "Matched",
            expectedCounts.Matched,
            actualCounts.Matched,
            failures);

        CompareStatusCount(
            "Mismatched",
            expectedCounts.Mismatched,
            actualCounts.Mismatched,
            failures);

        CompareStatusCount(
            "Missing",
            expectedCounts.Missing,
            actualCounts.Missing,
            failures);

        CompareStatusCount(
            "Duplicate",
            expectedCounts.Duplicate,
            actualCounts.Duplicate,
            failures);

        CompareStatusCount(
            "Unresolved",
            expectedCounts.Unresolved,
            actualCounts.Unresolved,
            failures);

        var expectedMatchRate =
            CalculateMatchRate(
                expectedCounts.Matched,
                expectedRows.Count);

        var actualMatchRate =
            CalculateMatchRate(
                actualCounts.Matched,
                actualResults.Count);

        if (expectedMatchRate != actualMatchRate)
        {
            failures.Add(
                $"Match rate mismatch. " +
                $"Expected {expectedMatchRate:0.00}, " +
                $"actual {actualMatchRate:0.00}.");
        }

        CompareReasonCodeCounts(
            expectedRows,
            actualResults,
            failures);

        CompareExceptionCategoryCounts(
            expectedRows,
            actualExceptions,
            failures);

        var expectedExceptionCount =
            expectedRows.Count(
                x => !string.IsNullOrWhiteSpace(
                    x.ExpectedExceptionCategory));

        if (expectedExceptionCount !=
            actualExceptions.Count)
        {
            failures.Add(
                $"Exception count mismatch. " +
                $"Expected {expectedExceptionCount}, " +
                $"actual {actualExceptions.Count}.");
        }

        return new GroundTruthComparisonResult
        {
            IsSuccess = failures.Count == 0,

            ExpectedTotalUnits =
                expectedRows.Count,

            ActualTotalUnits =
                actualResults.Count,

            ExpectedMatched =
                expectedCounts.Matched,

            ActualMatched =
                actualCounts.Matched,

            ExpectedMismatched =
                expectedCounts.Mismatched,

            ActualMismatched =
                actualCounts.Mismatched,

            ExpectedMissing =
                expectedCounts.Missing,

            ActualMissing =
                actualCounts.Missing,

            ExpectedDuplicate =
                expectedCounts.Duplicate,

            ActualDuplicate =
                actualCounts.Duplicate,

            ExpectedUnresolved =
                expectedCounts.Unresolved,

            ActualUnresolved =
                actualCounts.Unresolved,

            ExpectedMatchRate =
                expectedMatchRate,

            ActualMatchRate =
                actualMatchRate,

            Failures =
                failures
        };
    }

    private static void CompareTransactionLevelResults(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        List<string> failures)
    {
        var expectedByReference =
            expectedRows.ToDictionary(
                x => x.TransactionReference,
                StringComparer.Ordinal);

        var actualByReference =
            actualResults.ToDictionary(
                x => x.TransactionReference,
                StringComparer.Ordinal);

        foreach (var expected in expectedRows)
        {
            if (!actualByReference.TryGetValue(
                    expected.TransactionReference,
                    out var actual))
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    "missing from reconciliation results.");

                continue;
            }

            if (!string.Equals(
                    expected.ExpectedStatus,
                    actual.Status,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: status mismatch. " +
                    $"Expected '{expected.ExpectedStatus}', " +
                    $"actual '{actual.Status}'.");
            }

            if (!string.Equals(
                    expected.ExpectedReasonCode,
                    actual.ReasonCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: reason-code mismatch. " +
                    $"Expected '{expected.ExpectedReasonCode}', " +
                    $"actual '{actual.ReasonCode}'.");
            }
        }

        foreach (var actual in actualResults)
        {
            if (!expectedByReference.ContainsKey(
                    actual.TransactionReference))
            {
                failures.Add(
                    $"{actual.TransactionReference}: " +
                    "returned by reconciliation but missing from ground truth.");
            }
        }
    }

    private static void CompareTransactionLevelExceptions(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualException> actualExceptions,
        List<string> failures)
    {
        var expectedExceptionByReference =
            expectedRows
                .Where(
                    x => !string.IsNullOrWhiteSpace(
                        x.ExpectedExceptionCategory))
                .ToDictionary(
                    x => x.TransactionReference,
                    StringComparer.Ordinal);

        var actualExceptionByReference =
            new Dictionary<string, ActualException>(
                StringComparer.Ordinal);

        foreach (var exception in actualExceptions)
        {
            if (string.IsNullOrWhiteSpace(
                    exception.TransactionReference))
            {
                failures.Add(
                    $"Exception '{exception.ExceptionId}' " +
                    "does not contain a transaction reference.");

                continue;
            }

            if (!actualExceptionByReference.TryAdd(
                    exception.TransactionReference,
                    exception))
            {
                failures.Add(
                    $"{exception.TransactionReference}: " +
                    "multiple exceptions found for one reconciliation unit.");
            }
        }

        foreach (var expected in expectedRows)
        {
            var shouldHaveException =
                !string.IsNullOrWhiteSpace(
                    expected.ExpectedExceptionCategory);

            var hasException =
                actualExceptionByReference.ContainsKey(
                    expected.TransactionReference);

            if (shouldHaveException && !hasException)
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    $"expected exception '{expected.ExpectedExceptionCategory}' " +
                    "but no exception was returned.");

                continue;
            }

            if (!shouldHaveException && hasException)
            {
                failures.Add(
                    $"{expected.TransactionReference}: " +
                    "unexpected exception returned.");

                continue;
            }

            if (!shouldHaveException)
            {
                continue;
            }

            var actual =
                actualExceptionByReference[
                    expected.TransactionReference];

            if (!string.Equals(
                    expected.ExpectedExceptionCategory,
                    actual.Category,
                    StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{expected.TransactionReference}: exception category mismatch. " +
                    $"Expected '{expected.ExpectedExceptionCategory}', " +
                    $"actual '{actual.Category}'.");
            }
        }

        foreach (var actual in actualExceptions)
        {
            if (string.IsNullOrWhiteSpace(
                    actual.TransactionReference))
            {
                continue;
            }

            if (!expectedExceptionByReference.ContainsKey(
                    actual.TransactionReference))
            {
                failures.Add(
                    $"{actual.TransactionReference}: " +
                    "unexpected exception reference.");
            }
        }
    }

    private static void ValidateExpectedReferences(
        IReadOnlyList<GroundTruthRow> rows,
        List<string> failures)
    {
        var duplicates =
            rows.GroupBy(
                    x => x.TransactionReference,
                    StringComparer.Ordinal)
                .Where(
                    x => x.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            failures.Add(
                $"Ground truth contains duplicate transaction reference " +
                $"'{duplicate.Key}'.");
        }
    }

    private static void ValidateActualReferences(
        IReadOnlyList<ActualResult> rows,
        List<string> failures)
    {
        var duplicates =
            rows.GroupBy(
                    x => x.TransactionReference,
                    StringComparer.Ordinal)
                .Where(
                    x => x.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            failures.Add(
                $"Reconciliation results contain duplicate transaction reference " +
                $"'{duplicate.Key}'.");
        }
    }

    private static ExpectedStatusCounts BuildExpectedStatusCounts(
        IReadOnlyList<GroundTruthRow> rows)
    {
        return new ExpectedStatusCounts
        {
            Matched =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Matched",
                        StringComparison.OrdinalIgnoreCase)),

            Mismatched =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Mismatched",
                        StringComparison.OrdinalIgnoreCase)),

            Missing =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Missing",
                        StringComparison.OrdinalIgnoreCase)),

            Duplicate =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Duplicate",
                        StringComparison.OrdinalIgnoreCase)),

            Unresolved =
                rows.Count(
                    x => string.Equals(
                        x.ExpectedStatus,
                        "Unresolved",
                        StringComparison.OrdinalIgnoreCase))
        };
    }

    private static ActualStatusCounts BuildActualStatusCounts(
        IReadOnlyList<ActualResult> rows)
    {
        return new ActualStatusCounts
        {
            Matched =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Matched",
                        StringComparison.OrdinalIgnoreCase)),

            Mismatched =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Mismatched",
                        StringComparison.OrdinalIgnoreCase)),

            Missing =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Missing",
                        StringComparison.OrdinalIgnoreCase)),

            Duplicate =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Duplicate",
                        StringComparison.OrdinalIgnoreCase)),

            Unresolved =
                rows.Count(
                    x => string.Equals(
                        x.Status,
                        "Unresolved",
                        StringComparison.OrdinalIgnoreCase))
        };
    }

    private static void CompareStatusCount(
        string status,
        int expected,
        int actual,
        List<string> failures)
    {
        if (expected == actual)
        {
            return;
        }

        failures.Add(
            $"{status} count mismatch. " +
            $"Expected {expected}, actual {actual}.");
    }

    private static void CompareReasonCodeCounts(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualResult> actualResults,
        List<string> failures)
    {
        var expected =
            expectedRows
                .GroupBy(
                    x => x.ExpectedReasonCode,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var actual =
            actualResults
                .GroupBy(
                    x => x.ReasonCode,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var allCodes =
            expected.Keys.Union(
                actual.Keys,
                StringComparer.OrdinalIgnoreCase);

        foreach (var reasonCode in allCodes)
        {
            expected.TryGetValue(
                reasonCode,
                out var expectedCount);

            actual.TryGetValue(
                reasonCode,
                out var actualCount);

            if (expectedCount != actualCount)
            {
                failures.Add(
                    $"Reason code '{reasonCode}' mismatch. " +
                    $"Expected {expectedCount}, actual {actualCount}.");
            }
        }
    }

    private static void CompareExceptionCategoryCounts(
        IReadOnlyList<GroundTruthRow> expectedRows,
        IReadOnlyList<ActualException> actualExceptions,
        List<string> failures)
    {
        var expected =
            expectedRows
                .Where(
                    x => !string.IsNullOrWhiteSpace(
                        x.ExpectedExceptionCategory))
                .GroupBy(
                    x => x.ExpectedExceptionCategory,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var actual =
            actualExceptions
                .GroupBy(
                    x => x.Category,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count(),
                    StringComparer.OrdinalIgnoreCase);

        var allCategories =
            expected.Keys.Union(
                actual.Keys,
                StringComparer.OrdinalIgnoreCase);

        foreach (var category in allCategories)
        {
            expected.TryGetValue(
                category,
                out var expectedCount);

            actual.TryGetValue(
                category,
                out var actualCount);

            if (expectedCount != actualCount)
            {
                failures.Add(
                    $"Exception category '{category}' mismatch. " +
                    $"Expected {expectedCount}, actual {actualCount}.");
            }
        }
    }

    private static decimal CalculateMatchRate(
        int matched,
        int total)
    {
        if (total == 0)
        {
            return 0.00m;
        }

        return decimal.Round(
            matched * 100.00m / total,
            2);
    }

    private sealed class ExpectedStatusCounts
    {
        public int Matched { get; init; }

        public int Mismatched { get; init; }

        public int Missing { get; init; }

        public int Duplicate { get; init; }

        public int Unresolved { get; init; }
    }

    private sealed class ActualStatusCounts
    {
        public int Matched { get; init; }

        public int Mismatched { get; init; }

        public int Missing { get; init; }

        public int Duplicate { get; init; }

        public int Unresolved { get; init; }
    }
}
