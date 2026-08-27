using FinSight.DataGenerator.Generation;
using FinSight.DataGenerator.Validation;

// Portable output directory resolution.
//
// This tool is always documented/invoked as:
//     cd backend/FinSight.DataGenerator && dotnet run
// so the current working directory is the project directory, and the
// repository root is always two levels up from it. An optional
// FINSIGHT_OUTPUT_DIR environment variable overrides the default for
// CI or any other invocation convention, without changing zero-config
// behavior for the documented workflow.
var outputDirectoryOverride =
    Environment.GetEnvironmentVariable(
        "FINSIGHT_OUTPUT_DIR");

var outputDirectory =
    string.IsNullOrWhiteSpace(outputDirectoryOverride)
        ? Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "test-data",
                "generated"))
        : Path.GetFullPath(
            outputDirectoryOverride);

// ------------------------------------------------------------
// 1. Generate logical transactions
// ------------------------------------------------------------

var transactionGenerator =
    new TransactionGenerator();

var plannedTransactions =
    transactionGenerator.Generate();

// ------------------------------------------------------------
// 2. Generate raw source rows
// ------------------------------------------------------------

var sourceRowGenerator =
    new SourceRowGenerator();

var sourceRows =
    sourceRowGenerator.Generate(
        plannedTransactions);

// ------------------------------------------------------------
// 3. Generate independent ground truth
// ------------------------------------------------------------

var groundTruthGenerator =
    new GroundTruthGenerator();

var groundTruthRows =
    groundTruthGenerator.Generate(
        plannedTransactions);

// ------------------------------------------------------------
// 4. Write CSV files
// ------------------------------------------------------------

var csvWriter =
    new CsvWriter();

csvWriter.WriteAll(
    sourceRows,
    groundTruthRows,
    outputDirectory);

// ------------------------------------------------------------
// 5. Print generation summary
// ------------------------------------------------------------

var totalRawRows =
    sourceRows.Payments.Count +
    sourceRows.Banks.Count +
    sourceRows.Settlements.Count;

Console.WriteLine(
    "Data generation completed.");

Console.WriteLine();

Console.WriteLine(
    $"Logical transactions: {plannedTransactions.Count}");

Console.WriteLine(
    $"Payment rows: {sourceRows.Payments.Count}");

Console.WriteLine(
    $"Bank rows: {sourceRows.Banks.Count}");

Console.WriteLine(
    $"Settlement rows: {sourceRows.Settlements.Count}");

Console.WriteLine(
    $"Total raw rows: {totalRawRows}");

Console.WriteLine(
    $"Ground-truth rows: {groundTruthRows.Count}");

Console.WriteLine();

Console.WriteLine(
    $"Output directory: {outputDirectory}");

// ------------------------------------------------------------
// 6. Optional ground-truth comparison
// ------------------------------------------------------------

var runIdText =
    Environment.GetEnvironmentVariable(
        "FINSIGHT_RUN_ID");

if (!string.IsNullOrWhiteSpace(runIdText) &&
    Guid.TryParse(runIdText, out var runId))
{
    // The reconciliation endpoints are [Authorize]-protected -- the
    // comparator authenticates as a dedicated verification identity via
    // the real POST /api/auth/login endpoint (no [AllowAnonymous], no
    // hardcoded token). Credentials are supplied only via environment
    // variables, never committed to source control.
    var verifierEmail =
        Environment.GetEnvironmentVariable(
            "FINSIGHT_VERIFIER_EMAIL");

    var verifierPassword =
        Environment.GetEnvironmentVariable(
            "FINSIGHT_VERIFIER_PASSWORD");

    if (string.IsNullOrWhiteSpace(verifierEmail) ||
        string.IsNullOrWhiteSpace(verifierPassword))
    {
        Console.WriteLine();
        Console.WriteLine(
            "Ground-truth comparison skipped: FINSIGHT_VERIFIER_EMAIL " +
            "and FINSIGHT_VERIFIER_PASSWORD are both required to " +
            "authenticate against the [Authorize]-protected " +
            "reconciliation endpoints.");

        Environment.ExitCode = 1;
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Comparing reconciliation run: {runId}");

        var comparator =
            new GroundTruthComparator();

        try
        {
            var comparison =
                await comparator.CompareAsync(
                    "http://localhost:5180",
                    runId,
                    Path.Combine(
                        outputDirectory,
                        "ground-truth.csv"),
                    verifierEmail,
                    verifierPassword);

            comparison.Print();

            if (!comparison.IsSuccess)
            {
                Environment.ExitCode = 1;
            }
        }
        catch (GroundTruthAuthenticationException ex)
        {
            // Deliberately distinct from comparison.Print()'s output --
            // an authentication failure must never be mistaken for a
            // ground-truth data mismatch.
            Console.WriteLine();
            Console.WriteLine(
                $"GROUND-TRUTH VERIFIER AUTHENTICATION FAILED: " +
                ex.Message);

            Environment.ExitCode = 1;
        }
    }
}