namespace Porchlight.Core.Startup;

/// <summary>Plain-language advice about one startup item.</summary>
/// <param name="RecommendedToKeep">True for Windows/Microsoft items and Porchlight's own tools.</param>
/// <param name="Hint">Short "What is this?" sentence.</param>
public sealed record StartupClassification(bool RecommendedToKeep, string Hint);
