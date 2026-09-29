namespace Porchlight.Core.Browsers;

/// <summary>Overall plain-language verdict for an add-on. Advice only, never a malware verdict.</summary>
public enum ExtensionRiskLevel
{
    LooksFine,
    Review,
    WorthRemoving,
}
