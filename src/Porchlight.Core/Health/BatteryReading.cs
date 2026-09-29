namespace Porchlight.Core.Health;

/// <summary>Raw battery reading.</summary>
/// <param name="DesignCapacityMilliwattHours">Capacity when new (<c>BatteryStaticData.DesignedCapacity</c>).</param>
/// <param name="FullChargeCapacityMilliwattHours">Capacity it holds now when full
/// (<c>BatteryFullChargedCapacity.FullChargedCapacity</c>).</param>
/// <param name="CycleCount">Charge cycles, when the battery reports it.</param>
/// <param name="ChargePercent">Current charge, 0-100.</param>
/// <param name="State">Current charge state.</param>
public sealed record BatteryReading(
    long? DesignCapacityMilliwattHours,
    long? FullChargeCapacityMilliwattHours,
    int? CycleCount,
    int? ChargePercent,
    BatteryChargeState State);
