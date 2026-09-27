using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>
/// Display-ready row for one sensor on the sensors tab (current / min / max plus unit). Kept alive
/// and updated in place across ticks (see <see cref="UpdateFrom"/>) rather than recreated, so
/// binding a row's <see cref="IsVisible"/> to the filter box does not fight with the tree rebuilding
/// itself every second (S10).
/// </summary>
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

    public SensorRowViewModel(SensorReading reading)
    {
        Id = reading.Id;
        UpdateFrom(reading);
    }

    public string Id { get; }

    public void UpdateFrom(SensorReading reading)
    {
        Name = reading.Name;
        ValueText = Format(reading.Value, reading.Type);
        MinText = Format(reading.Min, reading.Type);
        MaxText = Format(reading.Max, reading.Type);
    }

    public bool Matches(string filter) => Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string Format(double? value, SensorType type)
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return "-";
        }

        if (type == SensorType.Throughput)
        {
            // LHM reports throughput in bytes/second; scale to whichever unit reads best.
            var bytesPerSecond = value.Value;
            return bytesPerSecond >= 1024 * 1024
                ? $"{(bytesPerSecond / (1024 * 1024)).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} MB/s"
                : $"{(bytesPerSecond / 1024).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} KB/s";
        }

        var unit = Unit(type);
        var digits = type is SensorType.Fan or SensorType.Clock ? "0" : "0.#";
        return $"{value.Value.ToString(digits, System.Globalization.CultureInfo.InvariantCulture)} {unit}".TrimEnd();
    }

    private static string Unit(SensorType type) => type switch
    {
        SensorType.Temperature => "C",
        SensorType.Fan => "RPM",
        SensorType.Load => "%",
        SensorType.Clock => "MHz",
        SensorType.Voltage => "V",
        SensorType.Power => "W",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Current => "A",
        SensorType.Energy => "mWh",
        SensorType.Level => "%",
        _ => string.Empty,
    };
}
