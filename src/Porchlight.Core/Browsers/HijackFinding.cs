namespace Porchlight.Core.Browsers;

/// <summary>The result of checking one setting in one browser (and profile, when the setting is per profile).</summary>
/// <param name="Browser">Which browser.</param>
/// <param name="ProfileName">The profile, or null when the finding is browser-wide (a policy).</param>
/// <param name="Setting">Which setting was checked.</param>
/// <param name="Status">The verdict.</param>
/// <param name="Value">Plain host name(s) to show, or a short phrase when nothing unusual is set.</param>
public sealed record HijackFinding(
    BrowserKind Browser,
    string? ProfileName,
    HijackSetting Setting,
    HijackStatus Status,
    string Value);
