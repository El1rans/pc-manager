namespace Porchlight.Core.Hardware;

/// <summary>Immutable, point-in-time view of every hardware node/sensor, published by
/// <see cref="IHardwareService"/> roughly once a second. The UI (and <c>FanControlManager</c>)
/// only ever sees these snapshots, never the underlying hardware library's own mutable types.</summary>
/// <param name="Status">Current access level.</param>
/// <param name="Message">User-readable detail for <see cref="HardwareStatus.Error"/>; null otherwise.</param>
/// <param name="Nodes">Root hardware nodes (CPU, GPU, Motherboard, Memory, Storage, Network, ...).</param>
/// <param name="TimestampUtc">When this snapshot was produced.</param>
public sealed record HardwareSnapshot(
    HardwareStatus Status,
    string? Message,
    IReadOnlyList<HardwareNode> Nodes,
    DateTimeOffset TimestampUtc)
{
    public static HardwareSnapshot Empty(HardwareStatus status, string? message = null) =>
        new(status, message, [], DateTimeOffset.UtcNow);

    /// <summary>Flattens every sensor in the tree, keyed by <see cref="SensorReading.Id"/>. Used by
    /// <c>FanControlManager</c> to look up fan-curve source temperatures without walking the tree
    /// itself, and by the sensors tab's filter box.</summary>
    public IEnumerable<SensorReading> AllSensors() => Flatten(Nodes);

    private static IEnumerable<SensorReading> Flatten(IReadOnlyList<HardwareNode> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var sensor in node.Sensors)
            {
                yield return sensor;
            }

            foreach (var sensor in Flatten(node.Children))
            {
                yield return sensor;
            }
        }
    }
}
