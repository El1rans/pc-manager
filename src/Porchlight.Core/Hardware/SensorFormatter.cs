using System.Globalization;

namespace Porchlight.Core.Hardware;

/// <summary>
/// Formats a <see cref="SensorReading"/>'s value into display text with the correct unit (spec 10:
/// "Every SensorType has a unit or explicit formatting"). Pure and side-effect free so it can be
/// unit tested directly, and shared by every sensor row on the Hardware page.
/// </summary>
public static class SensorFormatter
{
    private const string MissingValue = "-";

    /// <summary>Above this many volts, a voltage is shown with fewer decimals (e.g. a +12V rail)
    /// than a small rail like VCore, which needs 3 decimals to show meaningful change.</summary>
    private const double SmallVoltageThreshold = 6;

    /// <summary>Above this many MHz, a clock reads better as GHz (spec 10: "MHz/GHz").</summary>
    private const double GigahertzThreshold = 1000;

    /// <summary>Formats <paramref name="value"/> for display, including its unit. Returns "-" for a
    /// missing or NaN value.</summary>
    public static string Format(double? value, SensorType type)
    {
        if (value is null || double.IsNaN(value.Value))
        {
            return MissingValue;
        }

        var v = value.Value;
        return type switch
        {
            SensorType.Temperature => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} °C",
            SensorType.Fan => $"{v.ToString("0", CultureInfo.InvariantCulture)} RPM",
            SensorType.Load => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} %",
            SensorType.Clock => FormatClock(v),
            SensorType.Voltage => FormatVoltage(v),
            SensorType.Power => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} W",
            SensorType.Data => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} GB",
            SensorType.SmallData => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} MB",
            SensorType.Throughput => FormatThroughput(v),
            SensorType.Current => $"{v.ToString("0.##", CultureInfo.InvariantCulture)} A",
            SensorType.Energy => $"{v.ToString("0", CultureInfo.InvariantCulture)} mWh",
            SensorType.Level => $"{v.ToString("0.#", CultureInfo.InvariantCulture)} %",
            SensorType.Factor => $"{v.ToString("0.00", CultureInfo.InvariantCulture)}×",
            SensorType.Other => v.ToString("0.#", CultureInfo.InvariantCulture),
            _ => v.ToString("0.#", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>The unit alone (no value), used for section headings.</summary>
    public static string Unit(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Fan => "RPM",
        SensorType.Load => "%",
        SensorType.Clock => "MHz/GHz",
        SensorType.Voltage => "V",
        SensorType.Power => "W",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "KB/s",
        SensorType.Current => "A",
        SensorType.Energy => "mWh",
        SensorType.Level => "%",
        SensorType.Factor => "×",
        SensorType.Other => string.Empty,
        _ => string.Empty,
    };

    private static string FormatClock(double megahertz) =>
        megahertz >= GigahertzThreshold
            ? $"{(megahertz / GigahertzThreshold).ToString("0.00", CultureInfo.InvariantCulture)} GHz"
            : $"{megahertz.ToString("0", CultureInfo.InvariantCulture)} MHz";

    private static string FormatVoltage(double volts)
    {
        var digits = Math.Abs(volts) < SmallVoltageThreshold ? "0.000" : "0.00";
        return $"{volts.ToString(digits, CultureInfo.InvariantCulture)} V";
    }

    private static string FormatThroughput(double bytesPerSecond) =>
        Math.Abs(bytesPerSecond) >= 1024 * 1024
            ? $"{(bytesPerSecond / (1024 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB/s"
            : $"{(bytesPerSecond / 1024).ToString("0.#", CultureInfo.InvariantCulture)} KB/s";
}
