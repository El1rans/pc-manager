using PCManager.Core.Hardware;

namespace PCManager.App.Features.Hardware;

/// <summary>Display-ready row for one sensor on the sensors tab (current / min / max plus unit).</summary>
public sealed class SensorRowViewModel
{
    public SensorRowViewModel(SensorReading reading)
    {
        Id = reading.Id;
        Name = reading.Name;
        ValueText = Format(reading.Value, reading.Type);
        MinText = Format(reading.Min, reading.Type);
        MaxText = Format(reading.Max, reading.Type);
    }

    public string Id { get; }

    public string Name { get; }

    public string ValueText { get; }

    public string MinText { get; }

    public string MaxText { get; }

    private static string Format(double? value, SensorType type)
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return "-";
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
        _ => string.Empty,
    };
}
