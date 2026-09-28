using Porchlight.Core.Winget;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the Updates feature.</summary>
public sealed class UpdatesSettings
{
    /// <summary>Winget package ids the user never wants offered for update.</summary>
    public List<string> IgnoredIds { get; set; } = [];

    /// <summary>The last failed/attention-needing outcome for each package that has one, keyed by
    /// package id + the available version it was attempted against - see
    /// <see cref="PersistedUpdateOutcome"/> and <see cref="Winget.UpdateOutcomeMemory"/>. Lets the
    /// Updates page's row actions (Reinstall.../Hide/Try again) survive an app restart instead of
    /// only showing for the session that produced the failure. See
    /// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum.</summary>
    public List<PersistedUpdateOutcome> LastOutcomes { get; set; } = [];

    /// <summary>"Silent install (hide installer windows)" toggle.</summary>
    public bool Silent { get; set; }

    /// <summary>"Include apps with unknown version" toggle.</summary>
    public bool IncludeUnknown { get; set; } = true;

    /// <summary>
    /// "Check for updates when Porchlight starts" toggle. When true (the default),
    /// <c>UpdatesAutoCheckHostedService</c> runs a <c>winget upgrade</c> listing in the background
    /// on every app start, which contacts the winget package sources - see
    /// <c>docs/CODE_SIGNING_POLICY.md</c>'s privacy statement. When false, the first check only
    /// happens if/when the user opens the Updates page or clicks Refresh.
    /// </summary>
    public bool CheckOnStartup { get; set; } = true;
}
