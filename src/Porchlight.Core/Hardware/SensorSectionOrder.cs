namespace Porchlight.Core.Hardware;

/// <summary>
/// Groups and orders sensors by <see cref="SensorType"/> for the Hardware page's per-device cards
/// (spec 10: "Inside each card, sections by sensor type ... Temperatures, Fans, Load, Power,
/// Clocks, Voltages, then the rest"). Pure lookup tables so the ordering rule is unit testable
/// without any UI.
/// </summary>
public static class SensorSectionOrder
{
    /// <summary>Section order, first to last. Anything not listed falls under "Other" at the end.</summary>
    private static readonly SensorType[] Order =
    [
        SensorType.Temperature,
        SensorType.Fan,
        SensorType.Load,
        SensorType.Power,
        SensorType.Clock,
        SensorType.Voltage,
        SensorType.Data,
        SensorType.Throughput,
        SensorType.Factor,
        SensorType.Level,
        SensorType.Energy,
        SensorType.Current,
        SensorType.SmallData,
        SensorType.Other,
    ];

    /// <summary>Sort key for <paramref name="type"/>: lower sorts first. Every <see cref="SensorType"/>
    /// value has an explicit position in <see cref="Order"/>, so this never falls back to a default.</summary>
    public static int RankOf(SensorType type)
    {
        var index = Array.IndexOf(Order, type);
        return index >= 0 ? index : Order.Length;
    }

    /// <summary>Small heading shown above each non-empty section.</summary>
    public static string Heading(SensorType type) => type switch
    {
        SensorType.Temperature => "Temperatures",
        SensorType.Fan => "Fans",
        SensorType.Load => "Load",
        SensorType.Power => "Power",
        SensorType.Clock => "Clocks",
        SensorType.Voltage => "Voltages",
        SensorType.Data => "Data",
        SensorType.Throughput => "Throughput",
        SensorType.Factor => "Factors",
        SensorType.Level => "Levels",
        SensorType.Energy => "Energy",
        SensorType.Current => "Current",
        SensorType.SmallData => "Small data",
        SensorType.Other => "Other",
        _ => "Other",
    };
}
