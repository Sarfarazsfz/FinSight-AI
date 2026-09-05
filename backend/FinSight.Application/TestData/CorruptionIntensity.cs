namespace FinSight.Application.TestData;

/// <summary>
/// Fraction of logical transactions that receive deliberate corruption.
/// Ignored when <see cref="GenerationMode.Clean"/> is selected.
/// </summary>
public enum CorruptionIntensity
{
    /// <summary>~10 % of records corrupted.</summary>
    Low = 0,

    /// <summary>~20 % of records corrupted.</summary>
    Medium = 1,

    /// <summary>~30 % of records corrupted.</summary>
    High = 2
}
