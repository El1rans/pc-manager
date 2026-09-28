using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// One tile in the Hardware page's "At a glance" summary strip (spec 10), styled after the
/// Dashboard's <c>MetricTileViewModel</c> card. Status is never color alone (spec 00): a caution or
/// critical severity also swaps in a warning icon and text, not just a different brush.
/// </summary>
public sealed partial class HardwareSummaryTileViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _valueText = string.Empty;

    [ObservableProperty]
    private string? _detailText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarning))]
    [NotifyPropertyChangedFor(nameof(IsCritical))]
    [NotifyPropertyChangedFor(nameof(WarningIcon))]
    [NotifyPropertyChangedFor(nameof(WarningText))]
    private HardwareSummarySeverity _severity;

    public void UpdateFrom(HardwareSummaryTile tile)
    {
        Title = tile.Title;
        ValueText = tile.Value;
        DetailText = tile.Detail;
        Severity = tile.Severity;
    }

    public bool IsWarning => Severity != HardwareSummarySeverity.Normal;

    public bool IsCritical => Severity == HardwareSummarySeverity.Critical;

    public string WarningIcon => IsCritical ? "" : "";

    public string WarningText => Severity switch
    {
        HardwareSummarySeverity.Critical => "Critical",
        HardwareSummarySeverity.Caution => "Caution",
        _ => string.Empty,
    };
}
