using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 10: "Inside each card, sections by sensor type in this order: Temperatures, Fans,
/// Load, Power, Clocks, Voltages, then the rest".</summary>
public sealed class SensorSectionOrderTests
{
    [Fact]
    public void RankOf_MatchesTheSpecifiedOrder()
    {
        var expectedOrder = new[]
        {
            SensorType.Temperature,
            SensorType.Fan,
            SensorType.Load,
            SensorType.Power,
            SensorType.Clock,
            SensorType.Voltage,
        };

        for (var i = 1; i < expectedOrder.Length; i++)
        {
            Assert.True(
                SensorSectionOrder.RankOf(expectedOrder[i - 1]) < SensorSectionOrder.RankOf(expectedOrder[i]),
                $"{expectedOrder[i - 1]} should sort before {expectedOrder[i]}");
        }
    }

    [Fact]
    public void RankOf_TheRestSortsAfterTheSixNamedSections()
    {
        var namedSections = new[]
        {
            SensorType.Temperature, SensorType.Fan, SensorType.Load,
            SensorType.Power, SensorType.Clock, SensorType.Voltage,
        };
        var theRest = new[]
        {
            SensorType.Data, SensorType.Throughput, SensorType.Factor,
            SensorType.Level, SensorType.Energy, SensorType.Current, SensorType.SmallData, SensorType.Other,
        };

        var maxNamed = namedSections.Max(SensorSectionOrder.RankOf);
        foreach (var type in theRest)
        {
            Assert.True(SensorSectionOrder.RankOf(type) > maxNamed, $"{type} should sort after the six named sections");
        }
    }

    [Fact]
    public void RankOf_EveryEnumValue_HasAUniqueRank()
    {
        var ranks = Enum.GetValues<SensorType>().Select(SensorSectionOrder.RankOf).ToList();
        Assert.Equal(ranks.Count, ranks.Distinct().Count());
    }

    [Fact]
    public void Heading_EveryEnumValue_ReturnsANonEmptyHeading()
    {
        foreach (var type in Enum.GetValues<SensorType>())
        {
            Assert.False(string.IsNullOrWhiteSpace(SensorSectionOrder.Heading(type)));
        }
    }
}
