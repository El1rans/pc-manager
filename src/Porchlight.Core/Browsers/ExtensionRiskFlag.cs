namespace Porchlight.Core.Browsers;

/// <summary>One plain-language finding about an add-on: a short title and a one-sentence
/// explanation of why it matters.</summary>
public sealed record ExtensionRiskFlag(ExtensionRiskFlagKind Kind, string Title, string Explanation);
