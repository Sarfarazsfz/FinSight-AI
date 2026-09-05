using System.Security.Cryptography;
using FinSight.Application.Evaluation;

namespace FinSight.Application.TestData;

/// <summary>
/// Pure, stateless synthetic-data generator.
/// No database access, no production data, no external I/O.
///
/// Independence rule:
///   Ground-truth labels are derived from the <em>generation scenario plan</em>
///   (what the generator intended to create) — never from reconciliation output.
///   The reconciliation engine is exercised separately and compared against these
///   labels; a mismatch means the engine has a bug, not that the labels should
///   be adjusted.
/// </summary>
public sealed class SyntheticDataGenerator : ISyntheticDataGenerator
{
    // -----------------------------------------------------------------------
    // Allowed dataset sizes
    // -----------------------------------------------------------------------

    private static readonly int[] AllowedSizes = { 50, 100, 250, 500 };

    // -----------------------------------------------------------------------
    // Mixed-mode corruption weights (must sum to MixedTotalWeight)
    // Proportions mirror the canonical evaluator scenario (seed 42026).
    // -----------------------------------------------------------------------

    private const int MixedTotalWeight = 30;

    private static readonly (SyntheticScenario Scenario, int Weight)[] MixedWeights =
    {
        (SyntheticScenario.AmountMismatch,        8),
        (SyntheticScenario.DateMismatch,          2),
        (SyntheticScenario.MissingBank,           5),
        (SyntheticScenario.MissingSettlement,     4),
        (SyntheticScenario.MissingPayment,        3),
        (SyntheticScenario.DuplicatePayment,      3),
        (SyntheticScenario.DuplicateBank,         2),
        (SyntheticScenario.DuplicateSettlement,   1),
        (SyntheticScenario.UnresolvedReversedFraud, 2),
    };

    // Duplicate standalone mode: 3:2:1 split across the three duplicate types.
    private static readonly (SyntheticScenario Scenario, int Weight)[] DuplicateWeights =
    {
        (SyntheticScenario.DuplicatePayment,    3),
        (SyntheticScenario.DuplicateBank,       2),
        (SyntheticScenario.DuplicateSettlement, 1),
    };

    // All corruption types available for RandomChaos selection.
    private static readonly SyntheticScenario[] AllCorruptionTypes =
    {
        SyntheticScenario.AmountMismatch,
        SyntheticScenario.DateMismatch,
        SyntheticScenario.MissingBank,
        SyntheticScenario.MissingSettlement,
        SyntheticScenario.MissingPayment,
        SyntheticScenario.DuplicatePayment,
        SyntheticScenario.DuplicateBank,
        SyntheticScenario.DuplicateSettlement,
        SyntheticScenario.UnresolvedReversedFraud,
    };

    // -----------------------------------------------------------------------
    // Public entry point
    // -----------------------------------------------------------------------

    public DataGenerationResult Generate(DataGenerationRequest request)
    {
        ValidateRequest(request);

        var seed = request.Seed ?? NewCryptoSeed();
        var random = new Random((int)(seed & 0x7FFF_FFFF));

        // Build the ordered list of scenario assignments.
        var assignments = BuildAssignments(request, random);

        // Shuffle so corrupt records are not clustered at the end.
        Shuffle(assignments, random);

        // Generate transaction parameters (amounts, dates) with the seeded RNG.
        var transactions = CreateTransactions(assignments, random);

        // Generate source rows and independent ground truth in a single pass.
        var payments     = new List<GeneratedPaymentRow>(transactions.Count + 20);
        var banks        = new List<GeneratedBankRow>(transactions.Count + 20);
        var settlements  = new List<GeneratedSettlementRow>(transactions.Count + 20);
        var groundTruth  = new List<GroundTruthRow>(transactions.Count);

        foreach (var (txn, scenario) in transactions)
        {
            EmitSourceRows(payments, banks, settlements, txn, scenario);
            groundTruth.Add(BuildGroundTruthRow(txn, scenario));
        }

        var distribution = ComputeDistribution(transactions);
        var generationId = Guid.NewGuid().ToString("N");

        var metadata = new GeneratedDatasetMetadata
        {
            GenerationId       = generationId,
            Seed               = seed,
            Mode               = request.Mode,
            Size               = request.Size,
            Intensity          = request.Mode == GenerationMode.Clean
                                     ? null
                                     : request.Intensity,
            CreatedAt          = DateTimeOffset.UtcNow,
            ScenarioDistribution = distribution,
        };

        return new DataGenerationResult(metadata, payments, banks, settlements, groundTruth);
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    private static void ValidateRequest(DataGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!AllowedSizes.Contains(request.Size))
        {
            throw new ArgumentException(
                $"Size must be one of [{string.Join(", ", AllowedSizes)}].",
                nameof(request));
        }

        if (!Enum.IsDefined(request.Mode))
        {
            throw new ArgumentException(
                $"Unknown GenerationMode: {request.Mode}.",
                nameof(request));
        }

        if (!Enum.IsDefined(request.Intensity))
        {
            throw new ArgumentException(
                $"Unknown CorruptionIntensity: {request.Intensity}.",
                nameof(request));
        }
    }

    // -----------------------------------------------------------------------
    // Seed generation
    // -----------------------------------------------------------------------

    private static long NewCryptoSeed()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        // Keep positive (clamp to int range for Random ctor).
        return (long)BitConverter.ToUInt32(buf) & 0x7FFF_FFFF;
    }

    // -----------------------------------------------------------------------
    // Scenario plan
    // -----------------------------------------------------------------------

    private static List<SyntheticScenario> BuildAssignments(
        DataGenerationRequest request,
        Random random)
    {
        var n = request.Size;

        if (request.Mode == GenerationMode.Clean)
        {
            return Repeat(SyntheticScenario.ExactMatch, n);
        }

        var corruptCount = ComputeCorruptCount(n, request.Intensity);
        var cleanCount   = n - corruptCount;

        var corruptSlots = request.Mode switch
        {
            GenerationMode.AmountMismatch =>
                Repeat(SyntheticScenario.AmountMismatch, corruptCount),

            GenerationMode.DateMismatch =>
                Repeat(SyntheticScenario.DateMismatch, corruptCount),

            GenerationMode.MissingBank =>
                Repeat(SyntheticScenario.MissingBank, corruptCount),

            GenerationMode.MissingSettlement =>
                Repeat(SyntheticScenario.MissingSettlement, corruptCount),

            GenerationMode.MissingPayment =>
                Repeat(SyntheticScenario.MissingPayment, corruptCount),

            GenerationMode.Duplicate =>
                WeightedDistribute(DuplicateWeights, corruptCount),

            GenerationMode.Unresolved =>
                Repeat(SyntheticScenario.UnresolvedReversedFraud, corruptCount),

            GenerationMode.Mixed =>
                WeightedDistribute(MixedWeights, corruptCount),

            GenerationMode.RandomChaos =>
                BuildRandomChaosSlots(random, corruptCount),

            _ => throw new ArgumentOutOfRangeException(
                     nameof(request.Mode),
                     request.Mode,
                     "Unhandled GenerationMode.")
        };

        var result = new List<SyntheticScenario>(n);
        result.AddRange(Repeat(SyntheticScenario.ExactMatch, cleanCount));
        result.AddRange(corruptSlots);
        return result;
    }

    private static int ComputeCorruptCount(int size, CorruptionIntensity intensity) =>
        intensity switch
        {
            CorruptionIntensity.Low    => Math.Max(1, size / 10),
            CorruptionIntensity.Medium => Math.Max(2, size / 5),
            CorruptionIntensity.High   => Math.Max(3, (size * 3) / 10),
            _                          => 0
        };

    private static List<SyntheticScenario> BuildRandomChaosSlots(
        Random random,
        int corruptCount)
    {
        // Pick 3–5 distinct corruption types randomly (seeded).
        var pickCount = random.Next(3, 6); // 3,4,5
        var pool      = AllCorruptionTypes.ToList();
        Shuffle(pool, random);
        var chosen = pool.Take(pickCount).ToArray();

        // Build equal-weight distribution over the chosen types.
        var weights = chosen
            .Select(s => (s, Weight: 1))
            .ToArray();

        return WeightedDistribute(weights, corruptCount);
    }

    // -----------------------------------------------------------------------
    // Weighted distribution helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Distributes <paramref name="total"/> units across scenario types
    /// proportionally to their weights.  Remainder is added to the first
    /// entry to ensure counts sum exactly to <paramref name="total"/>.
    /// </summary>
    private static List<SyntheticScenario> WeightedDistribute(
        (SyntheticScenario Scenario, int Weight)[] weights,
        int total)
    {
        var totalWeight = weights.Sum(w => w.Weight);
        var result = new List<SyntheticScenario>(total);

        var assigned = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            var count = i == weights.Length - 1
                ? total - assigned   // last entry absorbs remainder
                : (int)Math.Floor((double)total * weights[i].Weight / totalWeight);

            for (var j = 0; j < count; j++)
            {
                result.Add(weights[i].Scenario);
            }

            assigned += count;
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Transaction creation
    // -----------------------------------------------------------------------

    private sealed record SyntheticTransaction(
        int SequenceNumber,
        string TransactionReference,
        decimal BaseAmount,
        DateOnly BaseDate);

    private static List<(SyntheticTransaction Txn, SyntheticScenario Scenario)>
        CreateTransactions(
            List<SyntheticScenario> assignments,
            Random random)
    {
        var result = new List<(SyntheticTransaction, SyntheticScenario)>(
            assignments.Count);

        var baseYear  = 2026;
        var baseMonth = 8;

        for (var i = 0; i < assignments.Count; i++)
        {
            var seq = i + 1;

            var amount =
                decimal.Round(
                    1000.00m + random.Next(1, 1000) * 10,
                    2);

            var date =
                new DateOnly(baseYear, baseMonth, 1)
                    .AddDays(random.Next(0, 28));

            var txn = new SyntheticTransaction(
                seq,
                $"TXN-{seq:0000}",
                amount,
                date);

            result.Add((txn, assignments[i]));
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Source row emission
    // -----------------------------------------------------------------------

    private static void EmitSourceRows(
        List<GeneratedPaymentRow>    payments,
        List<GeneratedBankRow>       banks,
        List<GeneratedSettlementRow> settlements,
        SyntheticTransaction txn,
        SyntheticScenario    scenario)
    {
        var amt  = txn.BaseAmount;
        var date = txn.BaseDate;
        var seq  = txn.SequenceNumber;
        var txnRef = txn.TransactionReference;

        switch (scenario)
        {
            case SyntheticScenario.ExactMatch:
                payments   .Add(Pay(seq,  txnRef, amt,       date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt,       date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt,       date, "SETTLED"));
                break;

            case SyntheticScenario.AmountMismatch:
                // Bank and settlement carry a £10 discrepancy.
                payments   .Add(Pay(seq,  txnRef, amt,         date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt - 10.00m, date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt - 10.00m, date, "SETTLED"));
                break;

            case SyntheticScenario.DateMismatch:
                // Bank and settlement are 2 days later (beyond tolerance).
                payments   .Add(Pay(seq,  txnRef, amt, date,             "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt, date.AddDays(2), "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt, date.AddDays(2), "SETTLED"));
                break;

            case SyntheticScenario.MissingBank:
                payments   .Add(Pay(seq, txnRef, amt, date, "COMPLETED"));
                // bank intentionally absent
                settlements.Add(Set(seq, txnRef, amt, date, "SETTLED"));
                break;

            case SyntheticScenario.MissingSettlement:
                payments.Add(Pay(seq,  txnRef, amt, date, "COMPLETED"));
                banks   .Add(Bank(seq, txnRef, amt, date, "CLEARED"));
                // settlement intentionally absent
                break;

            case SyntheticScenario.MissingPayment:
                // payment intentionally absent (orphan bank+settlement)
                banks      .Add(Bank(seq, txnRef, amt, date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt, date, "SETTLED"));
                break;

            case SyntheticScenario.DuplicatePayment:
                payments   .Add(Pay(seq,  txnRef, amt, date, "COMPLETED"));
                payments   .Add(DupPay(seq, txnRef, amt, date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt, date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt, date, "SETTLED"));
                break;

            case SyntheticScenario.DuplicateBank:
                payments   .Add(Pay(seq,  txnRef, amt, date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt, date, "CLEARED"));
                banks      .Add(DupBank(seq, txnRef, amt, date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt, date, "SETTLED"));
                break;

            case SyntheticScenario.DuplicateSettlement:
                payments   .Add(Pay(seq,  txnRef, amt, date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt, date, "CLEARED"));
                settlements.Add(Set(seq,  txnRef, amt, date, "SETTLED"));
                settlements.Add(DupSet(seq, txnRef, amt, date, "SETTLED"));
                break;

            case SyntheticScenario.UnresolvedReversedFraud:
                payments   .Add(Pay(seq,  txnRef, amt, date, "COMPLETED"));
                banks      .Add(Bank(seq, txnRef, amt, date, "REVERSED_FRAUD"));
                settlements.Add(Set(seq,  txnRef, amt, date, "SETTLED"));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unsupported SyntheticScenario.");
        }
    }

    // Row factory helpers
    private static GeneratedPaymentRow Pay(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"PAY-{seq:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    private static GeneratedPaymentRow DupPay(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"PAY-{seq + 10_000:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    private static GeneratedBankRow Bank(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"BANK-{seq:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    private static GeneratedBankRow DupBank(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"BANK-{seq + 10_000:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    private static GeneratedSettlementRow Set(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"SET-{seq:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    private static GeneratedSettlementRow DupSet(
        int seq, string txnRef, decimal amt, DateOnly date, string status)
        => new($"SET-{seq + 10_000:000000}", txnRef, decimal.Round(amt, 2), "INR", date, status);

    // -----------------------------------------------------------------------
    // Independent ground-truth generation
    //
    // Labels come SOLELY from the scenario intent — never from reconciliation
    // output.  See interface documentation for the correctness invariant.
    // -----------------------------------------------------------------------

    private static GroundTruthRow BuildGroundTruthRow(
        SyntheticTransaction txn,
        SyntheticScenario    scenario)
    {
        var txnRef = txn.TransactionReference;
        return scenario switch
        {
            SyntheticScenario.ExactMatch =>
                GT(txnRef, scenario, "Matched",    "EXACT_MATCH",              "",               true,  true,  true,  "Exact",                   "Exact"),

            SyntheticScenario.AmountMismatch =>
                GT(txnRef, scenario, "Mismatched", "AMOUNT_MISMATCH",          "AmountMismatch", true,  true,  true,  "BankAndSettlementMinus10", "Exact"),

            SyntheticScenario.DateMismatch =>
                GT(txnRef, scenario, "Mismatched", "DATE_OUT_OF_TOLERANCE",    "DateMismatch",   true,  true,  true,  "Exact",                   "+48h"),

            SyntheticScenario.MissingBank =>
                GT(txnRef, scenario, "Missing",    "SOURCE_ABSENT_BANK",       "MissingRecord",  true,  false, true,  "NotComparable",           "NotComparable"),

            SyntheticScenario.MissingSettlement =>
                GT(txnRef, scenario, "Missing",    "SOURCE_ABSENT_SETTLEMENT", "MissingRecord",  true,  true,  false, "NotComparable",           "NotComparable"),

            SyntheticScenario.MissingPayment =>
                GT(txnRef, scenario, "Missing",    "SOURCE_ABSENT_PAYMENT",    "MissingRecord",  false, true,  true,  "NotComparable",           "NotComparable"),

            SyntheticScenario.DuplicatePayment =>
                GT(txnRef, scenario, "Duplicate",  "DUPLICATE_PAYMENT",        "DuplicateRecord",true,  true,  true,  "Exact",                   "Exact"),

            SyntheticScenario.DuplicateBank =>
                GT(txnRef, scenario, "Duplicate",  "DUPLICATE_BANK",           "DuplicateRecord",true,  true,  true,  "Exact",                   "Exact"),

            SyntheticScenario.DuplicateSettlement =>
                GT(txnRef, scenario, "Duplicate",  "DUPLICATE_SETTLEMENT",     "DuplicateRecord",true,  true,  true,  "Exact",                   "Exact"),

            SyntheticScenario.UnresolvedReversedFraud =>
                GT(txnRef, scenario, "Unresolved", "UNRESOLVED",               "Unresolved",     true,  true,  true,  "Exact",                   "Exact"),

            _ => throw new ArgumentOutOfRangeException(
                     nameof(scenario),
                     scenario,
                     "Unsupported SyntheticScenario.")
        };
    }

    private static GroundTruthRow GT(
        string txnRef,
        SyntheticScenario scenario,
        string expectedStatus,
        string expectedReasonCode,
        string expectedExceptionCategory,
        bool   paymentPresent,
        bool   bankPresent,
        bool   settlementPresent,
        string amountRelationship,
        string dateRelationship)
        => new(
            txnRef,
            scenario.ToString(),
            expectedStatus,
            expectedReasonCode,
            expectedExceptionCategory,
            paymentPresent,
            bankPresent,
            settlementPresent,
            amountRelationship,
            dateRelationship);

    // -----------------------------------------------------------------------
    // Distribution summary
    // -----------------------------------------------------------------------

    private static IReadOnlyDictionary<string, int> ComputeDistribution(
        IReadOnlyList<(SyntheticTransaction, SyntheticScenario Scenario)> transactions)
    {
        var counts = new Dictionary<string, int>
        {
            ["Matched"]    = 0,
            ["Mismatched"] = 0,
            ["Missing"]    = 0,
            ["Duplicate"]  = 0,
            ["Unresolved"] = 0,
        };

        foreach (var (_, scenario) in transactions)
        {
            var bucket = scenario switch
            {
                SyntheticScenario.ExactMatch                => "Matched",
                SyntheticScenario.AmountMismatch            => "Mismatched",
                SyntheticScenario.DateMismatch              => "Mismatched",
                SyntheticScenario.MissingBank               => "Missing",
                SyntheticScenario.MissingSettlement         => "Missing",
                SyntheticScenario.MissingPayment            => "Missing",
                SyntheticScenario.DuplicatePayment          => "Duplicate",
                SyntheticScenario.DuplicateBank             => "Duplicate",
                SyntheticScenario.DuplicateSettlement       => "Duplicate",
                SyntheticScenario.UnresolvedReversedFraud   => "Unresolved",
                _ => throw new ArgumentOutOfRangeException(
                         nameof(scenario),
                         scenario,
                         null)
            };
            counts[bucket]++;
        }

        return counts;
    }

    // -----------------------------------------------------------------------
    // Utilities
    // -----------------------------------------------------------------------

    private static List<SyntheticScenario> Repeat(SyntheticScenario scenario, int count)
    {
        var list = new List<SyntheticScenario>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(scenario);
        }

        return list;
    }

    private static void Shuffle<T>(List<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
