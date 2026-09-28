namespace Porchlight.Core.Hardware;

/// <summary>Kind of reading a <see cref="SensorReading"/> carries. Determines its display unit
/// (Temperature: C, Fan: RPM, Load: %, Clock: MHz, Voltage: V, Power: W, Data: GB, Factor:
/// dimensionless, e.g. a multiplier or duty percent read back from the board).</summary>
public enum SensorType
{
    Temperature,
    Fan,
    Load,
    Clock,
    Voltage,
    Power,
    Data,
    SmallData,
    Throughput,
    Current,
    Energy,
    Level,
    Factor,
    Other,
}
