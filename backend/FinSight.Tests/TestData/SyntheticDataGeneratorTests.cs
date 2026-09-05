using FinSight.Application.TestData;

namespace FinSight.Tests.TestData;

/// <summary>
/// Deterministic unit tests for the parametrised synthetic-data generator.
///
/// These tests exercise only the Application-layer <see cref="SyntheticDataGenerator"/>
/// (the shared core used by both CLI and API).  No database, no HTTP, no I/O.
///
/// Ground-truth independence invariant:
///   Every test that verifies the expected scenario counts also verifies
///   that the ground-truth labels match those counts — derived from the
///   generation plan, never from reconciliation output.
/// </summary>
[TestFixture]
public sealed class SyntheticDataGeneratorTests
{
    private SyntheticDataGenerator _generator = null!;

    [SetUp]
    public void SetUp() => _generator = new SyntheticDataGenerator();

    // -----------------------------------------------------------------------
    // 1 — Same seed + same config → identical output
    // -----------------------------------------------------------------------

    [Test]
    public void SameSeedSameConfig_ProducesIdenticalOutput()
    {
        var request = new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 99_999,
        };

        var a = _generator.Generate(request);
        var b = _generator.Generate(request);

        Assert.That(a.Payments.Count,     Is.EqualTo(b.Payments.Count));
        Assert.That(a.Banks.Count,        Is.EqualTo(b.Banks.Count));
        Assert.That(a.Settlements.Count,  Is.EqualTo(b.Settlements.Count));
        Assert.That(a.GroundTruth.Count,  Is.EqualTo(b.GroundTruth.Count));

        for (var i = 0; i < a.GroundTruth.Count; i++)
        {
            Assert.That(
                b.GroundTruth[i].TransactionReference,
                Is.EqualTo(a.GroundTruth[i].TransactionReference),
                $"GT row {i} TransactionReference differs.");

            Assert.That(
                b.GroundTruth[i].ExpectedStatus,
                Is.EqualTo(a.GroundTruth[i].ExpectedStatus),
                $"GT row {i} ExpectedStatus differs.");

            Assert.That(
                b.GroundTruth[i].ExpectedReasonCode,
                Is.EqualTo(a.GroundTruth[i].ExpectedReasonCode),
                $"GT row {i} ExpectedReasonCode differs.");
        }

        Assert.That(a.Metadata.Seed, Is.EqualTo(b.Metadata.Seed));
    }

    // -----------------------------------------------------------------------
    // 2 — Different default generations → different seed
    // -----------------------------------------------------------------------

    [Test]
    public void TwoDefaultGenerations_ProduceDifferentSeeds()
    {
        // No explicit seed → generator must pick a new one each time.
        var request = new DataGenerationRequest
        {
            Size = 100,
            Mode = GenerationMode.Mixed,
        };

        var seeds = new HashSet<long>();
        for (var i = 0; i < 5; i++)
        {
            seeds.Add(_generator.Generate(request).Metadata.Seed);
        }

        // All 5 seeds should differ (astronomically unlikely to collide with 2^31 space).
        Assert.That(seeds.Count, Is.GreaterThan(1),
            "Multiple default generations produced the same seed — randomness is broken.");
    }

    // -----------------------------------------------------------------------
    // 3 — Different seed → different synthetic rows
    // -----------------------------------------------------------------------

    [Test]
    public void DifferentSeed_ProducesDifferentRows()
    {
        var base_request = new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
        };

        var a = _generator.Generate(base_request with { Seed = 11111 });
        var b = _generator.Generate(base_request with { Seed = 22222 });

        // Different seeds should produce at least some different amounts/dates.
        var amtsA = a.Payments.Select(p => p.Amount).ToList();
        var amtsB = b.Payments.Select(p => p.Amount).ToList();
        Assert.That(
            amtsA.SequenceEqual(amtsB),
            Is.False,
            "Different seeds produced identical payment amounts.");
    }

    // -----------------------------------------------------------------------
    // 4 — Clean mode → all matched, no exceptions
    // -----------------------------------------------------------------------

    [Test]
    public void CleanMode_AllRecordsMatchedInGroundTruth()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Clean,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 1,
        });

        Assert.That(result.GroundTruth.Count, Is.EqualTo(100));
        Assert.That(
            result.GroundTruth.All(r => r.ExpectedStatus == "Matched"),
            Is.True,
            "Clean mode produced non-Matched ground-truth rows.");

        Assert.That(result.Metadata.ExpectedMatched,    Is.EqualTo(100));
        Assert.That(result.Metadata.ExpectedMismatched, Is.EqualTo(0));
        Assert.That(result.Metadata.ExpectedMissing,    Is.EqualTo(0));
        Assert.That(result.Metadata.ExpectedDuplicate,  Is.EqualTo(0));
        Assert.That(result.Metadata.ExpectedUnresolved, Is.EqualTo(0));
    }

    // -----------------------------------------------------------------------
    // 5 — Amount mismatch mode → genuine Mismatched outcomes
    // -----------------------------------------------------------------------

    [Test]
    public void AmountMismatchMode_ProducesGenuineMismatches()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.AmountMismatch,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 2,
        });

        Assert.That(result.Metadata.ExpectedMismatched, Is.GreaterThan(0),
            "AmountMismatch mode produced zero Mismatched ground-truth rows.");

        // All mismatched rows should carry the AMOUNT_MISMATCH reason code.
        var mismatched = result.GroundTruth.Where(r => r.ExpectedStatus == "Mismatched").ToList();
        Assert.That(
            mismatched.All(r => r.ExpectedReasonCode == "AMOUNT_MISMATCH"),
            Is.True,
            "AmountMismatch mode produced Mismatched rows with wrong reason code.");

        // Bank/Settlement amounts in the source rows should differ from Payment amounts
        // for the mismatched transactions.
        var mismatchedRefs = mismatched.Select(r => r.TransactionReference).ToHashSet();
        foreach (var txnRef in mismatchedRefs)
        {
            var pay = result.Payments.FirstOrDefault(p => p.TransactionReference == txnRef);
            var bank = result.Banks.FirstOrDefault(b => b.TransactionReference == txnRef);
            if (pay is not null && bank is not null)
            {
                Assert.That(bank.Amount, Is.Not.EqualTo(pay.Amount),
                    $"TXN {txnRef}: bank amount should differ from payment amount for AmountMismatch.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // 6 — Date mismatch mode → genuine date-based mismatches
    // -----------------------------------------------------------------------

    [Test]
    public void DateMismatchMode_ProducesGenuineDateMismatches()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.DateMismatch,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 3,
        });

        Assert.That(result.Metadata.ExpectedMismatched, Is.GreaterThan(0));

        var mismatched = result.GroundTruth.Where(r => r.ExpectedStatus == "Mismatched").ToList();
        Assert.That(
            mismatched.All(r => r.ExpectedReasonCode == "DATE_OUT_OF_TOLERANCE"),
            Is.True);

        // Dates should differ (beyond tolerance).
        var mismatchedRefs = mismatched.Select(r => r.TransactionReference).ToHashSet();
        foreach (var txnRef in mismatchedRefs)
        {
            var pay  = result.Payments.FirstOrDefault(p => p.TransactionReference == txnRef);
            var bank = result.Banks.FirstOrDefault(b => b.TransactionReference == txnRef);
            if (pay is not null && bank is not null)
            {
                Assert.That(bank.Date, Is.Not.EqualTo(pay.Date),
                    $"TXN {txnRef}: bank date should differ from payment date for DateMismatch.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // 7 — Missing bank → bank absent from source rows
    // -----------------------------------------------------------------------

    [Test]
    public void MissingBankMode_BankRecordsAbsentForMissingTransactions()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.MissingBank,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 4,
        });

        Assert.That(result.Metadata.ExpectedMissing, Is.GreaterThan(0));

        var missingGT = result.GroundTruth
            .Where(r => r.ExpectedStatus == "Missing" &&
                        r.ExpectedReasonCode == "SOURCE_ABSENT_BANK")
            .ToList();

        Assert.That(missingGT.Count, Is.GreaterThan(0));

        foreach (var row in missingGT)
        {
            Assert.That(row.ExpectedBankPresent, Is.False,
                $"GT says bank absent but flag is true for {row.TransactionReference}");

            var bankRecord = result.Banks
                .FirstOrDefault(b => b.TransactionReference == row.TransactionReference);

            Assert.That(bankRecord, Is.Null,
                $"Bank record exists in source for {row.TransactionReference}, but GT says absent.");
        }
    }

    // -----------------------------------------------------------------------
    // 8 — Missing settlement → settlement absent from source rows
    // -----------------------------------------------------------------------

    [Test]
    public void MissingSettlementMode_SettlementAbsentForMissingTransactions()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.MissingSettlement,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 5,
        });

        Assert.That(result.Metadata.ExpectedMissing, Is.GreaterThan(0));

        var missingGT = result.GroundTruth
            .Where(r => r.ExpectedReasonCode == "SOURCE_ABSENT_SETTLEMENT")
            .ToList();

        foreach (var row in missingGT)
        {
            Assert.That(row.ExpectedSettlementPresent, Is.False);
            Assert.That(
                result.Settlements.Any(s => s.TransactionReference == row.TransactionReference),
                Is.False,
                $"Settlement exists but GT says absent for {row.TransactionReference}.");
        }
    }

    // -----------------------------------------------------------------------
    // 9 — Missing payment → payment absent (orphan bank+settlement)
    // -----------------------------------------------------------------------

    [Test]
    public void MissingPaymentMode_PaymentAbsentOrphanBankAndSettlementPresent()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.MissingPayment,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 6,
        });

        Assert.That(result.Metadata.ExpectedMissing, Is.GreaterThan(0));

        var missingGT = result.GroundTruth
            .Where(r => r.ExpectedReasonCode == "SOURCE_ABSENT_PAYMENT")
            .ToList();

        foreach (var row in missingGT)
        {
            Assert.That(row.ExpectedPaymentPresent, Is.False);

            Assert.That(
                result.Payments.Any(p => p.TransactionReference == row.TransactionReference),
                Is.False,
                $"Payment exists for {row.TransactionReference} but GT says absent.");

            // Orphan: bank and settlement should both exist.
            Assert.That(
                result.Banks.Any(b => b.TransactionReference == row.TransactionReference),
                Is.True,
                $"Bank absent for orphan {row.TransactionReference}.");

            Assert.That(
                result.Settlements.Any(s => s.TransactionReference == row.TransactionReference),
                Is.True,
                $"Settlement absent for orphan {row.TransactionReference}.");
        }
    }

    // -----------------------------------------------------------------------
    // 10 — Duplicate mode → duplicate records present
    // -----------------------------------------------------------------------

    [Test]
    public void DuplicateMode_DuplicateRecordsPresent()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Duplicate,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 7,
        });

        Assert.That(result.Metadata.ExpectedDuplicate, Is.GreaterThan(0));

        var dupGT = result.GroundTruth
            .Where(r => r.ExpectedStatus == "Duplicate")
            .ToList();

        Assert.That(dupGT.Count, Is.GreaterThan(0));

        foreach (var row in dupGT)
        {
            // For any duplicate transaction there should be more than one record
            // in at least one source (the duplicated source).
            var payCount = result.Payments.Count(p => p.TransactionReference == row.TransactionReference);
            var bankCount = result.Banks.Count(b => b.TransactionReference == row.TransactionReference);
            var setCount  = result.Settlements.Count(s => s.TransactionReference == row.TransactionReference);

            Assert.That(
                payCount > 1 || bankCount > 1 || setCount > 1,
                Is.True,
                $"No duplicate source records found for {row.TransactionReference}.");
        }
    }

    // -----------------------------------------------------------------------
    // 11 — Unresolved mode → genuine Unresolved outcomes
    // -----------------------------------------------------------------------

    [Test]
    public void UnresolvedMode_ProducesGenuineUnresolvedOutcomes()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Unresolved,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 8,
        });

        Assert.That(result.Metadata.ExpectedUnresolved, Is.GreaterThan(0));

        var unresolvedGT = result.GroundTruth
            .Where(r => r.ExpectedStatus == "Unresolved")
            .ToList();

        Assert.That(unresolvedGT.Count, Is.GreaterThan(0));

        // Bank status for unresolved should be REVERSED_FRAUD.
        foreach (var row in unresolvedGT)
        {
            var bankRecord = result.Banks
                .FirstOrDefault(b => b.TransactionReference == row.TransactionReference);

            Assert.That(bankRecord, Is.Not.Null,
                $"Bank record absent for Unresolved TXN {row.TransactionReference}.");

            Assert.That(bankRecord!.Status, Is.EqualTo("REVERSED_FRAUD"),
                $"Unresolved TXN {row.TransactionReference}: bank status should be REVERSED_FRAUD.");
        }
    }

    // -----------------------------------------------------------------------
    // 12 — Mixed mode → multiple valid categories present
    // -----------------------------------------------------------------------

    [Test]
    public void MixedMode_ProducesMultipleCorruptionCategories()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 9,
        });

        var statuses = result.GroundTruth
            .Select(r => r.ExpectedStatus)
            .Distinct()
            .ToHashSet();

        // Mixed should produce at least 3 distinct outcome categories.
        Assert.That(statuses.Count, Is.GreaterThanOrEqualTo(3),
            $"Mixed mode produced only {statuses.Count} distinct status categories.");

        Assert.That(statuses, Does.Contain("Matched"),
            "Mixed mode should always produce some Matched records.");
    }

    // -----------------------------------------------------------------------
    // 13 — Random chaos → reproducible from seed
    // -----------------------------------------------------------------------

    [Test]
    public void RandomChaosMode_SameSeedProducesIdenticalOutput()
    {
        var request = new DataGenerationRequest
        {
            Size      = 100,
            Mode      = GenerationMode.RandomChaos,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 77_777,
        };

        var a = _generator.Generate(request);
        var b = _generator.Generate(request);

        Assert.That(a.GroundTruth.Count, Is.EqualTo(b.GroundTruth.Count));

        for (var i = 0; i < a.GroundTruth.Count; i++)
        {
            Assert.That(
                b.GroundTruth[i].ExpectedStatus,
                Is.EqualTo(a.GroundTruth[i].ExpectedStatus),
                $"Row {i} ExpectedStatus differs between runs.");
        }
    }

    // -----------------------------------------------------------------------
    // 14 — Random chaos never creates structurally invalid rows
    // -----------------------------------------------------------------------

    [Test]
    public void RandomChaosMode_NeverProducesInvalidScenarioData()
    {
        var validStatuses  = new HashSet<string> { "Matched", "Mismatched", "Missing", "Duplicate", "Unresolved" };
        var validReasonCodes = new HashSet<string>
        {
            "EXACT_MATCH", "AMOUNT_MISMATCH", "DATE_OUT_OF_TOLERANCE",
            "SOURCE_ABSENT_BANK", "SOURCE_ABSENT_SETTLEMENT", "SOURCE_ABSENT_PAYMENT",
            "DUPLICATE_PAYMENT", "DUPLICATE_BANK", "DUPLICATE_SETTLEMENT",
            "UNRESOLVED",
        };

        for (var seed = 1L; seed <= 10L; seed++)
        {
            var result = _generator.Generate(new DataGenerationRequest
            {
                Size      = 100,
                Mode      = GenerationMode.RandomChaos,
                Intensity = CorruptionIntensity.Medium,
                Seed      = seed * 12345,
            });

            foreach (var row in result.GroundTruth)
            {
                Assert.That(validStatuses, Does.Contain(row.ExpectedStatus),
                    $"Seed {seed}: invalid ExpectedStatus '{row.ExpectedStatus}'.");

                Assert.That(validReasonCodes, Does.Contain(row.ExpectedReasonCode),
                    $"Seed {seed}: invalid ExpectedReasonCode '{row.ExpectedReasonCode}'.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // 15 — Ground truth is independent of reconciliation output
    // -----------------------------------------------------------------------

    [Test]
    public void GroundTruth_IsProducedFromScenarioPlan_NotReconciliationOutput()
    {
        // The ground truth generator is a pure function of the scenario assignments.
        // We verify this structurally: for a MissingBank scenario, the GT must say
        // SOURCE_ABSENT_BANK even though no reconciliation has been run.
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 50,
            Mode      = GenerationMode.MissingBank,
            Intensity = CorruptionIntensity.High,
            Seed      = 42,
        });

        // Locate the ground-truth rows that claim SOURCE_ABSENT_BANK.
        var bankAbsent = result.GroundTruth
            .Where(r => r.ExpectedReasonCode == "SOURCE_ABSENT_BANK")
            .ToList();

        Assert.That(bankAbsent.Count, Is.GreaterThan(0),
            "Expected at least one SOURCE_ABSENT_BANK row in MissingBank/High scenario.");

        // Confirm those same transaction references truly have no bank record.
        foreach (var gt in bankAbsent)
        {
            var hasBank = result.Banks
                .Any(b => b.TransactionReference == gt.TransactionReference);

            Assert.That(
                hasBank,
                Is.False,
                $"GT says bank absent but bank record exists for {gt.TransactionReference}. " +
                "Ground truth must be derived from the scenario plan, not from reconciliation output.");
        }
    }

    // -----------------------------------------------------------------------
    // 16 — Canonical CLI scenario (seed 42026) is unchanged
    //       (exercises the original CLI generator directly)
    // -----------------------------------------------------------------------

    [Test]
    public void CanonicalCliScenario_Seed42026_Unchanged()
    {
        // The CLI generator (FinSight.DataGenerator project) is the canonical
        // evaluator scenario.  We verify it still produces the expected
        // distribution that was verified during the evaluator phase.
        var generator = new FinSight.DataGenerator.Generation.TransactionGenerator();
        var planned   = generator.Generate();

        var matched    = planned.Count(p => p.Scenario is
            FinSight.DataGenerator.Models.ReconciliationScenario.ExactMatch or
            FinSight.DataGenerator.Models.ReconciliationScenario.ToleranceMatch);

        var mismatched = planned.Count(p => p.Scenario is
            FinSight.DataGenerator.Models.ReconciliationScenario.AmountMismatch or
            FinSight.DataGenerator.Models.ReconciliationScenario.DateMismatch);

        var missing    = planned.Count(p => p.Scenario is
            FinSight.DataGenerator.Models.ReconciliationScenario.MissingBank or
            FinSight.DataGenerator.Models.ReconciliationScenario.MissingSettlement or
            FinSight.DataGenerator.Models.ReconciliationScenario.MissingPayment);

        var duplicate  = planned.Count(p => p.Scenario is
            FinSight.DataGenerator.Models.ReconciliationScenario.DuplicatePayment or
            FinSight.DataGenerator.Models.ReconciliationScenario.DuplicateBank or
            FinSight.DataGenerator.Models.ReconciliationScenario.DuplicateSettlement);

        var unresolved = planned.Count(p =>
            p.Scenario == FinSight.DataGenerator.Models.ReconciliationScenario.UnresolvedReversedFraud);

        Assert.That(matched,    Is.EqualTo(70), "Canonical: matched count changed.");
        Assert.That(mismatched, Is.EqualTo(10), "Canonical: mismatched count changed.");
        Assert.That(missing,    Is.EqualTo(12), "Canonical: missing count changed.");
        Assert.That(duplicate,  Is.EqualTo(6),  "Canonical: duplicate count changed.");
        Assert.That(unresolved, Is.EqualTo(2),  "Canonical: unresolved count changed.");
    }

    // -----------------------------------------------------------------------
    // 17-20 — Size variants
    // -----------------------------------------------------------------------

    [TestCase(50)]
    [TestCase(100)]
    [TestCase(250)]
    [TestCase(500)]
    public void SupportedSize_GeneratesCorrectLogicalTransactionCount(int size)
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = size,
            Mode      = GenerationMode.Mixed,
            Intensity = CorruptionIntensity.Medium,
            Seed      = (long)size * 7,
        });

        Assert.That(result.GroundTruth.Count, Is.EqualTo(size),
            $"Expected {size} ground-truth rows.");

        var scenarioTotal =
            result.Metadata.ExpectedMatched +
            result.Metadata.ExpectedMismatched +
            result.Metadata.ExpectedMissing +
            result.Metadata.ExpectedDuplicate +
            result.Metadata.ExpectedUnresolved;

        Assert.That(scenarioTotal, Is.EqualTo(size),
            $"Scenario distribution totals {scenarioTotal}, expected {size}.");
    }

    // -----------------------------------------------------------------------
    // Additional: invalid size rejected
    // -----------------------------------------------------------------------

    [Test]
    public void InvalidSize_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.Generate(new DataGenerationRequest { Size = 999 }));
    }

    // -----------------------------------------------------------------------
    // Additional: metadata records the actual seed used
    // -----------------------------------------------------------------------

    [Test]
    public void ExplicitSeed_MetadataRecordsIt()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size = 50,
            Seed = 12345678L,
        });

        Assert.That(result.Metadata.Seed, Is.EqualTo(12345678L));
    }

    [Test]
    public void NullSeed_MetadataContainsNonZeroSeed()
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size = 50,
            Seed = null,
        });

        Assert.That(result.Metadata.Seed, Is.GreaterThan(0));
    }

    // -----------------------------------------------------------------------
    // Regression: duplicate record IDs must use numeric offset not 'D' prefix
    // (PAY-D000001 breaks the ingestion validator ^PAY-\d{6}$ regex)
    // -----------------------------------------------------------------------

    [Test]
    [TestCase(GenerationMode.Duplicate)]
    [TestCase(GenerationMode.Mixed)]
    [TestCase(GenerationMode.RandomChaos)]
    public void DuplicateModes_AllIds_PassIngestionValidatorFormat(GenerationMode mode)
    {
        var result = _generator.Generate(new DataGenerationRequest
        {
            Size      = 100,
            Mode      = mode,
            Intensity = CorruptionIntensity.Medium,
            Seed      = 42026L,
        });

        // All payment, bank, and settlement IDs must match only digits after the prefix.
        var payIdRegex  = new System.Text.RegularExpressions.Regex(@"^PAY-\d{6}$");
        var bankIdRegex = new System.Text.RegularExpressions.Regex(@"^BANK-\d{6}$");
        var setIdRegex  = new System.Text.RegularExpressions.Regex(@"^SET-\d{6}$");

        foreach (var row in result.Payments)
        {
            Assert.That(payIdRegex.IsMatch(row.PaymentRecordId),
                Is.True,
                $"Payment id '{row.PaymentRecordId}' does not match ^PAY-\\d{{6}}$ (mode={mode})");
        }

        foreach (var row in result.Banks)
        {
            Assert.That(bankIdRegex.IsMatch(row.BankRecordId),
                Is.True,
                $"Bank id '{row.BankRecordId}' does not match ^BANK-\\d{{6}}$ (mode={mode})");
        }

        foreach (var row in result.Settlements)
        {
            Assert.That(setIdRegex.IsMatch(row.SettlementRecordId),
                Is.True,
                $"Settlement id '{row.SettlementRecordId}' does not match ^SET-\\d{{6}}$ (mode={mode})");
        }
    }
}
