namespace PCManager.Core.Hardware;

/// <summary>Top-level grouping for the sensors tree (spec 04: "tree grouped by hardware").</summary>
public enum HardwareNodeType
{
    Cpu,
    Gpu,
    Motherboard,
    Memory,
    Storage,
    Network,
    Other,
}
