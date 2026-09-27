using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Winget;

namespace PCManager.App.Features.Updates;

/// <summary>One row of the Updates page's DataGrid: a <see cref="WingetPackage"/> plus the
/// selection/ignore/live-status state the page tracks for it.</summary>
public sealed partial class UpdatePackageViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Notes))]
    private bool _isIgnored;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    private UpdateRowState _state = UpdateRowState.None;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public UpdatePackageViewModel(WingetPackage package)
    {
        Package = package;
    }

    public WingetPackage Package { get; }

    public string Name => Package.Name;

    public string Id => Package.Id;

    public string InstalledVersion => Package.InstalledVersion;

    public string AvailableVersion => Package.AvailableVersion;

    public bool RequiresExplicit => Package.RequiresExplicit;

    /// <summary>"Ignored" / "Pinned / explicit only" / "Current version unknown", in that priority
    /// order - see <c>docs/specs/02-updates.md</c>.</summary>
    public string Notes
    {
        get
        {
            if (IsIgnored)
            {
                return "Ignored";
            }

            if (RequiresExplicit)
            {
                return "Pinned / explicit only";
            }

            if (InstalledVersion.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "Current version unknown";
            }

            return string.Empty;
        }
    }

    public string StatusGlyph => State switch
    {
        UpdateRowState.Updated => "", // checkmark
        UpdateRowState.Failed => "", // error
        UpdateRowState.Updating => "", // sync/progress
        UpdateRowState.Skipped => "", // warning
        UpdateRowState.Queued => "", // clock
        _ => string.Empty,
    };
}
