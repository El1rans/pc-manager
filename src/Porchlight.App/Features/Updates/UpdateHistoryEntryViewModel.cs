using System.Globalization;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

/// <summary>One row of the history list: formats an <see cref="UpdateHistoryEntry"/> into the plain
/// text the Updates page shows (app, version change, what was done, result, time). Status is always
/// a glyph plus text, never color alone. See <c>docs/specs/26-update-history.md</c>.</summary>
public sealed class UpdateHistoryEntryViewModel
{
    /// <summary>Segoe Fluent Icons "CheckMark" / "ErrorBadge".</summary>
    private const string SuccessGlyph = "";
    private const string FailureGlyph = "";

    public const string DoneText = "Done";

    public const string FailedText = "Didn't work";

    private readonly UpdateHistoryEntry _entry;

    public UpdateHistoryEntryViewModel(UpdateHistoryEntry entry, TimeZoneInfo zone)
    {
        _entry = entry;
        var local = TimeZoneInfo.ConvertTime(entry.TimestampUtc, zone);
        TimeText = local.ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    public string PackageName => _entry.PackageName;

    /// <summary>"1.2.0 → 1.3.1", or just the new version when the old one is unknown; empty when neither is known.</summary>
    public string VersionText
    {
        get
        {
            var from = _entry.FromVersion;
            var to = _entry.ToVersion;
            if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            {
                return $"{from} → {to}";
            }

            return !string.IsNullOrWhiteSpace(to) ? to : from ?? string.Empty;
        }
    }

    /// <summary>"Updated", "Reinstalled" or "Installed".</summary>
    public string ActionText => _entry.Action switch
    {
        UpdateHistoryAction.Reinstall => "Reinstalled",
        UpdateHistoryAction.Install => "Installed",
        _ => "Updated",
    };

    public bool Succeeded => _entry.Succeeded;

    public string Glyph => Succeeded ? SuccessGlyph : FailureGlyph;

    /// <summary>"Done", or "Didn't work" plus the friendly outcome title.</summary>
    public string ResultText => Succeeded || string.IsNullOrWhiteSpace(_entry.OutcomeTitle)
        ? (Succeeded ? DoneText : FailedText)
        : $"{FailedText} - {_entry.OutcomeTitle}";

    public string TimeText { get; }

    /// <summary>The friendly explanation the live row showed (with the winget code); empty on success.</summary>
    public string Explanation => Succeeded || string.IsNullOrWhiteSpace(_entry.Explanation)
        ? string.Empty
        : $"{_entry.Explanation} (winget code 0x{unchecked((uint)_entry.ExitCode).ToString("X8", CultureInfo.InvariantCulture)})";

    /// <summary>For screen readers, e.g. "Zoom, updated to 6.2.1, done, 14:05".</summary>
    public string AutomationName
    {
        get
        {
            var to = string.IsNullOrWhiteSpace(_entry.ToVersion) ? string.Empty : " to " + _entry.ToVersion;
            var what = ActionText.ToLower(CultureInfo.CurrentCulture) + to;
            return Succeeded
                ? $"{PackageName}, {what}, {DoneText.ToLower(CultureInfo.CurrentCulture)}, {TimeText}"
                : $"{PackageName}, {what}, {ResultText.ToLower(CultureInfo.CurrentCulture)}, {TimeText}";
        }
    }
}
