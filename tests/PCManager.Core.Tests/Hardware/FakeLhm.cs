using LibreHardwareMonitor.Hardware;

namespace PCManager.Core.Tests.Hardware;

/// <summary>
/// Minimal fakes for the three LHM interfaces <see cref="PCManager.Core.Hardware.HardwareService.BuildNode"/>
/// consumes, so B1's control/RPM pairing logic can be exercised without a real driver or hardware.
/// Only the members that method actually reads are implemented meaningfully; everything else throws
/// if touched, so a test fails loudly if it starts depending on something new.
/// </summary>
internal sealed class FakeLhmControl : IControl
{
    public FakeLhmControl(ISensor sensor)
    {
        Sensor = sensor;
        Identifier = new Identifier(sensor.Identifier, "control");
    }

    public ControlMode ControlMode { get; set; } = ControlMode.Default;

    public Identifier Identifier { get; }

    public float MaxSoftwareValue { get; set; } = 100;

    public float MinSoftwareValue { get; set; }

    public ISensor Sensor { get; }

    public float SoftwareValue { get; private set; }

    public void SetDefault() => ControlMode = ControlMode.Default;

    public void SetSoftware(float value)
    {
        ControlMode = ControlMode.Software;
        SoftwareValue = value;
    }
}

internal sealed class FakeLhmSensor : ISensor
{
    public FakeLhmSensor(string path, string name, SensorType sensorType, int index, IHardware hardware)
    {
        Identifier = new Identifier(path);
        Name = name;
        SensorType = sensorType;
        Index = index;
        Hardware = hardware;
    }

    public IControl? Control { get; set; }

    public IHardware Hardware { get; }

    public Identifier Identifier { get; }

    public int Index { get; }

    public bool IsDefaultHidden => false;

    public float? Max { get; set; }

    public float? Min { get; set; }

    public string Name { get; set; }

    public IReadOnlyList<IParameter> Parameters { get; } = [];

    public SensorType SensorType { get; }

    public float? Value { get; set; }

    public IEnumerable<SensorValue> Values => [];

    public TimeSpan ValuesTimeWindow { get; set; }

    public void ClearValues()
    {
    }

    public void ResetMax()
    {
    }

    public void ResetMin()
    {
    }

    public void Accept(IVisitor visitor) => throw new NotSupportedException("Not used by BuildNode.");

    public void Traverse(IVisitor visitor) => throw new NotSupportedException("Not used by BuildNode.");
}

internal sealed class FakeLhmHardware : IHardware
{
    public FakeLhmHardware(string path, string name, HardwareType hardwareType)
    {
        Identifier = new Identifier(path);
        Name = name;
        HardwareType = hardwareType;
    }

    public List<FakeLhmSensor> SensorList { get; } = [];

    public List<FakeLhmHardware> SubHardwareList { get; } = [];

    public HardwareType HardwareType { get; }

    public Identifier Identifier { get; }

    public string Name { get; set; }

    public IHardware? Parent => null;

    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    public ISensor[] Sensors => [.. SensorList];

    public IHardware[] SubHardware => [.. SubHardwareList];

#pragma warning disable CS0067 // never raised by this fake - BuildNode does not subscribe to these
    public event SensorEventHandler? SensorAdded;
    public event SensorEventHandler? SensorRemoved;
#pragma warning restore CS0067

    public string GetReport() => string.Empty;

    public void Update()
    {
    }

    public void Accept(IVisitor visitor) => throw new NotSupportedException("Not used by BuildNode.");

    public void Traverse(IVisitor visitor) => throw new NotSupportedException("Not used by BuildNode.");
}
