namespace Porchlight.Core.Browsers;

/// <summary>An installed add-on together with its risk assessment.</summary>
public sealed record AssessedExtension(InstalledExtension Extension, ExtensionRiskAssessment Risk);
