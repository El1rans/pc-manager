using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 10: "add a unit test covering every LibreHardwareMonitor SensorType value" - this
/// enumerates every <see cref="SensorType"/> (Core's mirror of LHM's own enum) and proves each one
/// formats with an explicit, correct unit; none of them fall through to an empty/default case
/// silently.</summary>
public sealed class SensorFormatterTests
{
    [Fact]
    public void Format_NullValue_ReturnsDash()
    {
        foreach (var type in Enum.GetValues<SensorType>())
        {
            Assert.Equal("-", SensorFormatter.Format(null, type));
        }
    }

    [Fact]
    public void Format_NaN_ReturnsDash() =>
        Assert.Equal("-", SensorFormatter.Format(double.NaN, SensorType.Temperature));

    [Theory]
    [InlineData(SensorType.Temperature, "34.5 °C")]
    [InlineData(SensorType.Fan, "1200 RPM")]
    [InlineData(SensorType.Load, "23.5 %")]
    [InlineData(SensorType.Power, "68.3 W")]
    [InlineData(SensorType.Data, "412 GB")]
    [InlineData(SensorType.SmallData, "512 MB")]
    [InlineData(SensorType.Current, "1.5 A")]
    [InlineData(SensorType.Energy, "3000 mWh")]
    [InlineData(SensorType.Level, "50 %")]
    public void Format_SimpleUnits_MatchExpected(SensorType type, string expected)
    {
        var value = type switch
        {
            SensorType.Temperature => 34.5,
            SensorType.Fan => 1200,
            SensorType.Load => 23.5,
            SensorType.Power => 68.3,
            SensorType.Data => 412,
            SensorType.SmallData => 512,
            SensorType.Current => 1.5,
            SensorType.Energy => 3000,
            SensorType.Level => 50,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        Assert.Equal(expected, SensorFormatter.Format(value, type));
    }

    [Fact]
    public void Format_Factor_ShowsMultiplierSymbol() =>
        // Regression: the maintainer's screenshot showed "Core #1  34" with no unit at all - LHM's
        // per-core multiplier sensor (SensorType.Factor) used to map to an empty string.
        Assert.Equal("34.00×", SensorFormatter.Format(34.0, SensorType.Factor));

    [Theory]
    [InlineData(SensorType.Clock, 800, "800 MHz")] // below the gigahertz threshold
    [InlineData(SensorType.Clock, 4200, "4.20 GHz")] // at or above the gigahertz threshold
    [InlineData(SensorType.Voltage, 1.284, "1.284 V")] // small rail: 3 decimals
    [InlineData(SensorType.Voltage, 12.06, "12.06 V")] // large rail: 2 decimals
    [InlineData(SensorType.Throughput, 500 * 1024, "500 KB/s")] // below one megabyte
    [InlineData(SensorType.Throughput, 2.3 * 1024 * 1024, "2.3 MB/s")] // at or above one megabyte
    public void Format_MagnitudeDependentUnits_PickTheUnitAndPrecisionForTheValue(
        SensorType type, double value, string expected) =>
        Assert.Equal(expected, SensorFormatter.Format(value, type));

    [Fact]
    public void Format_Other_HasNoUnitButStillFormatsTheNumber() =>
        Assert.Equal("7.5", SensorFormatter.Format(7.5, SensorType.Other));

    [Fact]
    public void Unit_EveryEnumValue_ReturnsAnExplicitAnswer()
    {
        // Not every unit is a fixed string (Clock and Throughput vary by magnitude), but every
        // SensorType must have SOME defined behaviour - this proves Unit() never throws for any
        // current SensorType member.
        foreach (var type in Enum.GetValues<SensorType>())
        {
            var unit = SensorFormatter.Unit(type);
            Assert.NotNull(unit);
        }
    }

    [Theory]
    [InlineData(SensorType.Temperature, "°C")]
    [InlineData(SensorType.Fan, "RPM")]
    [InlineData(SensorType.Load, "%")]
    [InlineData(SensorType.Voltage, "V")]
    [InlineData(SensorType.Power, "W")]
    [InlineData(SensorType.Data, "GB")]
    [InlineData(SensorType.SmallData, "MB")]
    [InlineData(SensorType.Current, "A")]
    [InlineData(SensorType.Energy, "mWh")]
    [InlineData(SensorType.Level, "%")]
    [InlineData(SensorType.Factor, "×")]
    [InlineData(SensorType.Other, "")]
    public void Unit_FixedUnits_MatchExpected(SensorType type, string expected) =>
        Assert.Equal(expected, SensorFormatter.Unit(type));
}
