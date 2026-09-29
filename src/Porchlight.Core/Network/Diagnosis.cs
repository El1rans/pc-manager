namespace Porchlight.Core.Network;

/// <summary>The troubleshooter's conclusion in plain words, plus the fixes that fit it.</summary>
/// <param name="Outcome">What was concluded.</param>
/// <param name="Headline">One short sentence.</param>
/// <param name="Advice">What to do next, in plain language.</param>
/// <param name="Remedies">Fixes to offer, best first. Empty when nothing needs fixing.</param>
public sealed record Diagnosis(
    DiagnosisOutcome Outcome, string Headline, string Advice, IReadOnlyList<RemedyKind> Remedies);
