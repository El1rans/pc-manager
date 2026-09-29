namespace Porchlight.Core.Browsers;

/// <summary>The overall level plus the individual findings behind it.</summary>
public sealed record ExtensionRiskAssessment(ExtensionRiskLevel Level, IReadOnlyList<ExtensionRiskFlag> Flags);
