namespace FinSight.Application.TestData;

/// <summary>
/// Generates fully synthetic payment/bank/settlement CSVs and an
/// independent ground-truth file for reconciliation evaluation and demo.
///
/// Ground truth is derived from the generation intent (the scenario plan),
/// NOT from running the reconciliation engine.  This ensures the generated
/// test matrix is a genuine correctness check rather than a circular proof.
/// </summary>
public interface ISyntheticDataGenerator
{
    /// <summary>
    /// Generates a complete dataset.
    ///
    /// When <paramref name="request"/>.<see cref="DataGenerationRequest.Seed"/>
    /// is null a cryptographically random seed is chosen and recorded in
    /// <see cref="DataGenerationResult.Metadata"/>.  Passing the same seed
    /// and request shape produces byte-identical output.
    /// </summary>
    DataGenerationResult Generate(DataGenerationRequest request);
}
