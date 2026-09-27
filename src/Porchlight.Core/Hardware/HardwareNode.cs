namespace Porchlight.Core.Hardware;

/// <summary>One node in the sensors tree: a piece of hardware (or a sub-part of one, e.g. a CPU
/// core) with its own sensors and child nodes.</summary>
/// <param name="Id">Stable identifier (LHM hardware identifier string in the real adapter).</param>
/// <param name="Name">Display name, e.g. "AMD Ryzen 7 5800X".</param>
/// <param name="Type">Which top-level group this belongs under.</param>
/// <param name="Sensors">This node's own sensors.</param>
/// <param name="Children">Sub-nodes (e.g. per-core temperature groups).</param>
public sealed record HardwareNode(
    string Id,
    string Name,
    HardwareNodeType Type,
    IReadOnlyList<SensorReading> Sensors,
    IReadOnlyList<HardwareNode> Children);
