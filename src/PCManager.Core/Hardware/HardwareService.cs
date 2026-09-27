using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;
using PCManager.Core.Components;
using PCManager.Core.Elevation;

namespace PCManager.Core.Hardware;

/// <inheritdoc cref="IHardwareService"/>
/// <remarks>
/// Deliberately thin: this class only opens/updates/closes LHM's <see cref="Computer"/> and
/// translates its tree into plain <see cref="HardwareSnapshot"/> data. Every safety/decision rule
/// lives in <see cref="FanControlEngine"/> instead - nothing here decides a fan speed.
/// </remarks>
public sealed class HardwareService : IHardwareService, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IComponentService _componentService;
    private readonly IElevationService _elevationService;
    private readonly ILogger<HardwareService> _logger;
    private readonly UpdateVisitor _updateVisitor = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly object _startStopLock = new();

    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _reinitRequested;
    private volatile bool _resetMinMaxRequested;
    private volatile bool _pawnioInstalled;
    private Computer? _computer;

    public HardwareService(IComponentService componentService, IElevationService elevationService, ILogger<HardwareService> logger)
    {
        _componentService = componentService;
        _elevationService = elevationService;
        _logger = logger;
        _componentService.StatusChanged += OnComponentStatusChanged;
        Latest = HardwareSnapshot.Empty(InitialStatus());
    }

    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareSnapshot Latest { get; private set; }

    public IReadOnlyList<IFanController> Controllers { get; private set; } = [];

    public void ResetMinMax()
    {
        _resetMinMaxRequested = true;
        _wake.Set();
    }

    public void Start()
    {
        lock (_startStopLock)
        {
            if (_thread is not null)
            {
                return;
            }

            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "PCManager.Hardware" };
            _thread.Start();
        }
    }

    public void Stop()
    {
        lock (_startStopLock)
        {
            if (_thread is null)
            {
                return;
            }

            _running = false;
            _wake.Set();
            _thread.Join(TimeSpan.FromSeconds(5));
            _thread = null;
        }
    }

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentStatusChanged;
        Stop();
        _wake.Dispose();
    }

    private HardwareStatus InitialStatus() => _elevationService.IsElevated ? HardwareStatus.DriverMissing : HardwareStatus.NotElevated;

    private void OnComponentStatusChanged(object? sender, ComponentStatusChangeEventInfo e)
    {
        if (e.ComponentId != ComponentIds.PawnIo)
        {
            return;
        }

        _pawnioInstalled = e.Status.State is ComponentState.Installed or ComponentState.Running;
        _reinitRequested = true;
        _wake.Set();
    }

    /// <summary>The dedicated background thread body. Every call into LHM happens from here.</summary>
    private void Run()
    {
        try
        {
            _pawnioInstalled = _componentService.GetStatusAsync(ComponentIds.PawnIo, CancellationToken.None)
                .GetAwaiter().GetResult().State is ComponentState.Installed or ComponentState.Running;
        }
        catch (Exception ex)
        {
            // Best-effort initial read; the loop below re-derives status every tick regardless, and
            // a StatusChanged event will correct this if it arrives later.
            _logger.LogWarning(ex, "Could not read initial PawnIO component status.");
        }

        OpenComputer();

        while (_running)
        {
            try
            {
                if (_reinitRequested)
                {
                    _reinitRequested = false;
                    CloseComputer();
                    OpenComputer();
                }

                Tick();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hardware read failed.");
                Publish(HardwareSnapshot.Empty(HardwareStatus.Error, "Could not read hardware sensors. Check the log for details."));
            }

            _wake.Wait(PollInterval);
            _wake.Reset();
        }

        CloseComputer();
    }

    private void OpenComputer()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
                IsControllerEnabled = true,
                IsPsuEnabled = true,
            };
            _computer.Open();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the hardware monitor.");
            _computer = null;
            Publish(HardwareSnapshot.Empty(HardwareStatus.Error, "Could not open the hardware monitor. Check the log for details."));
        }
    }

    private void CloseComputer()
    {
        if (_computer is null)
        {
            return;
        }

        try
        {
            foreach (var controller in Controllers)
            {
                // Never leave a fan under software control just because we are re-initializing or
                // shutting down the reader - rule 5.
                controller.RestoreDefault();
            }

            _computer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing the hardware monitor.");
        }
        finally
        {
            _computer = null;
            Controllers = [];
        }
    }

    private void Tick()
    {
        if (_computer is null)
        {
            Publish(HardwareSnapshot.Empty(ComputeStatus(), "Could not open the hardware monitor."));
            return;
        }

        if (_resetMinMaxRequested)
        {
            _resetMinMaxRequested = false;
            ResetMinMax(_computer.Hardware);
        }

        _computer.Accept(_updateVisitor);

        var controllers = new List<IFanController>();
        var nodes = _computer.Hardware.Select(hw => BuildNode(hw, controllers)).ToList();
        Controllers = controllers;

        Publish(new HardwareSnapshot(ComputeStatus(), null, nodes, DateTimeOffset.UtcNow));
    }

    private HardwareStatus ComputeStatus()
    {
        if (!_elevationService.IsElevated)
        {
            return HardwareStatus.NotElevated;
        }

        return _pawnioInstalled ? HardwareStatus.Ready : HardwareStatus.DriverMissing;
    }

    private static void ResetMinMax(IEnumerable<IHardware> hardware)
    {
        foreach (var hw in hardware)
        {
            foreach (var sensor in hw.Sensors)
            {
                sensor.ResetMin();
                sensor.ResetMax();
            }

            ResetMinMax(hw.SubHardware);
        }
    }

    private static HardwareNode BuildNode(IHardware hardware, List<IFanController> controllers)
    {
        var sensors = new List<SensorReading>();
        var now = DateTimeOffset.UtcNow;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Control)
            {
                // Duty-cycle percent already surfaces through IFanController.CurrentPercent;
                // showing it again in the sensors tree would just duplicate the fan card.
                continue;
            }

            if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Fan && sensor.Control is not null)
            {
                controllers.Add(new LhmFanController(sensor));
            }

            sensors.Add(new SensorReading(
                sensor.Identifier.ToString(),
                sensor.Name,
                MapSensorType(sensor.SensorType),
                sensor.Value,
                sensor.Min,
                sensor.Max,
                now));
        }

        var children = hardware.SubHardware.Select(sub => BuildNode(sub, controllers)).ToList();

        return new HardwareNode(
            hardware.Identifier.ToString(),
            hardware.Name,
            MapNodeType(hardware.HardwareType),
            sensors,
            children);
    }

    private static HardwareNodeType MapNodeType(LibreHardwareMonitor.Hardware.HardwareType type) => type switch
    {
        LibreHardwareMonitor.Hardware.HardwareType.Cpu => HardwareNodeType.Cpu,
        LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia or
        LibreHardwareMonitor.Hardware.HardwareType.GpuAmd or
        LibreHardwareMonitor.Hardware.HardwareType.GpuIntel => HardwareNodeType.Gpu,
        LibreHardwareMonitor.Hardware.HardwareType.Motherboard or
        LibreHardwareMonitor.Hardware.HardwareType.SuperIO or
        LibreHardwareMonitor.Hardware.HardwareType.EmbeddedController => HardwareNodeType.Motherboard,
        LibreHardwareMonitor.Hardware.HardwareType.Memory => HardwareNodeType.Memory,
        LibreHardwareMonitor.Hardware.HardwareType.Storage => HardwareNodeType.Storage,
        LibreHardwareMonitor.Hardware.HardwareType.Network => HardwareNodeType.Network,
        _ => HardwareNodeType.Other,
    };

    private static SensorType MapSensorType(LibreHardwareMonitor.Hardware.SensorType type) => type switch
    {
        LibreHardwareMonitor.Hardware.SensorType.Temperature => SensorType.Temperature,
        LibreHardwareMonitor.Hardware.SensorType.Fan => SensorType.Fan,
        LibreHardwareMonitor.Hardware.SensorType.Load => SensorType.Load,
        LibreHardwareMonitor.Hardware.SensorType.Clock or
        LibreHardwareMonitor.Hardware.SensorType.Frequency => SensorType.Clock,
        LibreHardwareMonitor.Hardware.SensorType.Voltage => SensorType.Voltage,
        LibreHardwareMonitor.Hardware.SensorType.Power => SensorType.Power,
        LibreHardwareMonitor.Hardware.SensorType.Data or
        LibreHardwareMonitor.Hardware.SensorType.SmallData or
        LibreHardwareMonitor.Hardware.SensorType.Throughput => SensorType.Data,
        LibreHardwareMonitor.Hardware.SensorType.Factor => SensorType.Factor,
        _ => SensorType.Other,
    };

    private void Publish(HardwareSnapshot snapshot)
    {
        Latest = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
    }

    /// <summary>Standard LHM traversal helper: visiting the computer traverses into every hardware
    /// node and calls <see cref="IHardware.Update"/> on each (including sub-hardware).</summary>
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
            {
                sub.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }
}
