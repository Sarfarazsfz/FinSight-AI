namespace FinSight.Application.TestData;

/// <summary>
/// Inputs to the parametrised synthetic-data generator.
/// </summary>
public sealed record DataGenerationRequest
{
    /// <summary>
    /// Number of logical transactions (50, 100, 250, or 500).
    /// Each logical transaction produces one or more source rows
    /// depending on the scenario (e.g. duplicates add an extra row).
    /// </summary>
    public int Size { get; init; } = 100;

    /// <summary>The corruption mode to apply.</summary>
    public GenerationMode Mode { get; init; } = GenerationMode.Mixed;

    /// <summary>
    /// Fraction of transactions to corrupt.
    /// Ignored when <see cref="GenerationMode.Clean"/> is selected.
    /// </summary>
    public CorruptionIntensity Intensity { get; init; } = CorruptionIntensity.Medium;

    /// <summary>
    /// Optional seed for reproducible generation.
    /// When null the generator picks a new cryptographically random seed
    /// and returns it in <see cref="GeneratedDatasetMetadata.Seed"/> so
    /// the exact dataset can be recreated later.
    /// </summary>
    public long? Seed { get; init; }
}
