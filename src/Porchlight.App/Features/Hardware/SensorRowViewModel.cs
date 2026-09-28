using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// Display-ready row for one sensor on the sensors tab (current / min / max plus unit). Kept alive
/// and updated in place across ticks (see <see cref="UpdateFrom"/>) rather than recreated, so
/// binding a row's <see cref="IsVisible"/> to the filter box does not fight with the tree rebuilding
/// itself every second (S10).
/// </summary>
/// <remarks>
/// Spec 10: <see cref="ValueText"/> (Current) is the primary, most prominent text on the row - bound
/// with no dim/secondary style in <c>HardwareView.xaml</c> - while <see cref="MinText"/>/
/// <see cref="MaxText"/> use the secondary brush. Formatting itself is delegated to
/// <see cref="SensorFormatter"/> so every <see cref="SensorType"/> gets a correct, consistent unit.
/// </remarks>
public sealed partial class SensorRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _valueText = "-";

    [ObservableProperty]
    private string _minText = "-";

    [ObservableProperty]
    private string _maxText = "-";

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>Whether this sensor has ever reported a real (non-zero, non-null) value - see
    /// <see cref="UnusedSensorTracker"/>. Drives <see cref="IsVisible"/> together with the filter
    /// text when "Hide unused sensors" is on.</summary>
    [ObservableProperty]
    private bool _isUsed = true;

    public SensorRowViewModel(SensorReading reading, bool isUsed, string? displayNameOverride = null)
    {
        Id = reading.Id;
        UpdateFrom(reading, isUsed, displayNameOverride);
    }

    public string Id { get; }

    public SensorType Type { get; private set; }

    /// <param name="displayNameOverride">Fans polish addendum: the fan's custom display name
    /// (<see cref="Porchlight.Core.Hardware.FanNaming"/>), when this row is a fan's RPM sensor and
    /// the user has renamed it. Null for every non-fan sensor, and for a fan with no custom name.</param>
    public void UpdateFrom(SensorReading reading, bool isUsed, string? displayNameOverride = null)
    {
        Name = displayNameOverride ?? reading.Name;
        Type = reading.Type;
        ValueText = SensorFormatter.Format(reading.Value, reading.Type);
        MinText = SensorFormatter.Format(reading.Min, reading.Type);
        MaxText = SensorFormatter.Format(reading.Max, reading.Type);
        IsUsed = isUsed;
    }

    public bool MatchesName(string filter) => Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Recomputes <see cref="IsVisible"/> from the filter text, the "Hide unused sensors"
    /// toggle, and whether an ancestor (the device card or this sensor's section) already matched
    /// the filter by name - in which case every sensor under it shows regardless of its own name.</summary>
    public void ApplyVisibility(string filter, bool hideUnused, bool ancestorMatched)
    {
        var matchesFilter = ancestorMatched || string.IsNullOrEmpty(filter) || MatchesName(filter);
        IsVisible = matchesFilter && (!hideUnused || IsUsed);
    }
}
