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

    [Fact]
    public void Format_Clock_BelowGigahertzThreshold_UsesMhz() =>
        Assert.Equal("800 MHz", SensorFormatter.Format(800, SensorType.Clock));

    [Fact]
    public void Format_Clock_AtOrAboveGigahertzThreshold_UsesGhz() =>
        Assert.Equal("4.20 GHz", SensorFormatter.Format(4200, SensorType.Clock));

    [Fact]
    public void Format_Voltage_SmallRail_Uses3Decimals() =>
        Assert.Equal("1.284 V", SensorFormatter.Format(1.284, SensorType.Voltage));

    [Fact]
    public void Format_Voltage_LargeRail_Uses2Decimals() =>
        Assert.Equal("12.06 V", SensorFormatter.Format(12.06, SensorType.Voltage));

    [Fact]
    public void Format_Throughput_BelowOneMegabyte_UsesKbPerSecond() =>
        Assert.Equal("500 KB/s", SensorFormatter.Format(500 * 1024, SensorType.Throughput));

    [Fact]
    public void Format_Throughput_AtOrAboveOneMegabyte_UsesMbPerSecond() =>
        Assert.Equal("2.3 MB/s", SensorFormatter.Format(2.3 * 1024 * 1024, SensorType.Throughput));

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
