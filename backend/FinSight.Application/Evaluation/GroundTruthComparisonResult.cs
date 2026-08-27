namespace FinSight.Application.Evaluation;

public sealed class GroundTruthComparisonResult
{
    public bool IsSuccess { get; init; }

    public int ExpectedTotalUnits { get; init; }

    public int ActualTotalUnits { get; init; }

    public int ExpectedMatched { get; init; }

    public int ActualMatched { get; init; }

    public int ExpectedMismatched { get; init; }

    public int ActualMismatched { get; init; }

    public int ExpectedMissing { get; init; }

    public int ActualMissing { get; init; }

    public int ExpectedDuplicate { get; init; }

    public int ActualDuplicate { get; init; }

    public int ExpectedUnresolved { get; init; }

    public int ActualUnresolved { get; init; }

    public decimal ExpectedMatchRate { get; init; }

    public decimal ActualMatchRate { get; init; }

    public IReadOnlyList<string> Failures { get; init; } =
        Array.Empty<string>();

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine(
            "===== GROUND TRUTH COMPARISON =====");

        Console.WriteLine();

        Console.WriteLine(
            $"Total Units : " +
            $"{ActualTotalUnits}/{ExpectedTotalUnits}");

        Console.WriteLine(
            $"Matched     : " +
            $"{ActualMatched}/{ExpectedMatched}");

        Console.WriteLine(
            $"Mismatched  : " +
            $"{ActualMismatched}/{ExpectedMismatched}");

        Console.WriteLine(
            $"Missing     : " +
            $"{ActualMissing}/{ExpectedMissing}");

        Console.WriteLine(
            $"Duplicate   : " +
            $"{ActualDuplicate}/{ExpectedDuplicate}");

        Console.WriteLine(
            $"Unresolved  : " +
            $"{ActualUnresolved}/{ExpectedUnresolved}");

        Console.WriteLine(
            $"Match Rate  : " +
            $"{ActualMatchRate:0.00}% / " +
            $"{ExpectedMatchRate:0.00}%");

        Console.WriteLine();

        if (IsSuccess)
        {
            Console.WriteLine(
                "TRANSACTION-LEVEL GROUND TRUTH: PASS");

            return;
        }

        Console.WriteLine(
            "TRANSACTION-LEVEL GROUND TRUTH: FAIL");

        Console.WriteLine();

        foreach (var failure in Failures)
        {
            Console.WriteLine(
                $" - {failure}");
        }
    }
}
