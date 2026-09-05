namespace FinSight.Application.TestData;

/// <summary>
/// Describes a generated synthetic dataset.  Safe to expose in API responses
/// — contains no secrets, no production data, no credentials.
/// </summary>
public sealed record GeneratedDatasetMetadata
{
    /// <summary>
    /// Opaque server-side identifier.  Used to reference this dataset
    /// in download endpoints for up to one hour after generation.
    /// </summary>
    public string GenerationId { get; init; } = string.Empty;

    /// <summary>
    /// The seed that was used (or auto-generated) for this dataset.
    /// Pass the same seed with the same request parameters to reproduce
    /// the exact same CSV files.
    /// </summary>
    public long Seed { get; init; }

    /// <summary>Generation mode.</summary>
    public GenerationMode Mode { get; init; }

    /// <summary>Number of logical transactions requested.</summary>
    public int Size { get; init; }

    /// <summary>
    /// Corruption intensity that was applied.
    /// Null when Mode is <see cref="GenerationMode.Clean"/>.
    /// </summary>
    public CorruptionIntensity? Intensity { get; init; }

    /// <summary>UTC timestamp of generation.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// How many logical transactions fell into each scenario.
    /// These are EXPECTED outcomes based on the generator's intent,
    /// not actual reconciliation results.
    /// </summary>
    public IReadOnlyDictionary<string, int> ScenarioDistribution { get; init; } =
        new Dictionary<string, int>();

    // Derived convenience properties for the API response.
    public int ExpectedMatched =>
        ScenarioDistribution.TryGetValue("Matched", out var v) ? v : 0;

    public int ExpectedMismatched =>
        ScenarioDistribution.TryGetValue("Mismatched", out var v) ? v : 0;

    public int ExpectedMissing =>
        ScenarioDistribution.TryGetValue("Missing", out var v) ? v : 0;

    public int ExpectedDuplicate =>
        ScenarioDistribution.TryGetValue("Duplicate", out var v) ? v : 0;

    public int ExpectedUnresolved =>
        ScenarioDistribution.TryGetValue("Unresolved", out var v) ? v : 0;
}
