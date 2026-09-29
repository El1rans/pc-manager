namespace Porchlight.Core.Health;

/// <summary>The plain-language result of evaluating one disk.</summary>
/// <param name="Verdict">Overall verdict.</param>
/// <param name="VerdictText">"Healthy", "Warning - back up your files soon" or "Unknown".</param>
/// <param name="Reasons">Plain sentences explaining a warning (empty when healthy).</param>
public sealed record DiskHealthAssessment(
    DiskHealthVerdict Verdict, string VerdictText, IReadOnlyList<string> Reasons);
