using System.Collections.Concurrent;
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

    /// <summary>How long a sensor's numeric value can stay exactly unchanged before it is stamped
    /// with its *original* observation time rather than "now" - see <see cref="BuildNode"/>. A
    /// genuinely live temperature/RPM reading drifts by at least a little within a few seconds
    /// under any real workload; a value frozen for longer than the rule-3 stale threshold is a
    /// reasonable proxy for "this sensor stopped updating", which is exactly the failure rule 3
    /// exists to catch.</summary>
    private readonly Dictionary<string, (double? Value, DateTimeOffset ObservedUtc)> _lastObserved = [];

    private readonly IComponentService _componentService;
    private readonly IElevationService _elevationService;
    private readonly ILogger<HardwareService> _logger;
    private readonly UpdateVisitor _updateVisitor = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly ConcurrentQueue<Action> _ownerThreadCommands = new();
    private readonly object _startStopLock = new();

    private Thread? _thread;
    private volatile bool _threadStillRunning;
    private volatile bool _running;
    private volatile bool _reinitRequested;
    private volatile bool _resetMinMaxRequested;
    private volatile bool _pawnioInstalled;
    private volatile bool _driverConfirmedActive;
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

    public void RunOnOwnerThread(Action action, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Thread.CurrentThread == _thread)
        {
            action();
            return;
        }

        using var done = new ManualResetEventSlim(false);
        _ownerThreadCommands.Enqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Owner-thread command failed.");
            }
            finally
            {
                done.Set();
            }
        });
        _wake.Set();

        if (done.Wait(timeout))
        {
            return;
        }

        // The hardware thread did not pick this up in time (stuck in a long native call, or not
        // running at all). A safety action - restoring fans to default - must still happen
        // somewhere rather than never running, so fall back to a direct call even though it
        // breaks the "one thread" rule as a last resort. The queued copy may still run later and
        // will just find the fans already restored.
        _logger.LogWarning("Owner-thread command timed out after {Timeout}; running it directly as a fallback.", timeout);
        action();
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
            _threadStillRunning = true;
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
            var stoppedInTime = _thread.Join(TimeSpan.FromSeconds(5));
            if (!stoppedInTime)
            {
                // S6: the thread may still be inside _wake.Wait() (or a slow native call) past our
                // join timeout. _threadStillRunning stays true so Dispose() knows not to touch
                // _wake - the thread might still call Wait()/Reset() on it, and disposing it from
                // here would risk an ObjectDisposedException on a background thread we no longer
                // control.
                _logger.LogWarning("Hardware thread did not stop within the shutdown timeout.");
                _thread = null;
                return;
            }

            _threadStillRunning = false;
            _thread = null;
        }
    }

    public void Dispose()
    {
        _componentService.StatusChanged -= OnComponentStatusChanged;
        Stop();
        if (!_threadStillRunning)
        {
            _wake.Dispose();
        }
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
            DrainOwnerThreadCommands();

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

        DrainOwnerThreadCommands();
        CloseComputer();
    }

    private void DrainOwnerThreadCommands()
    {
        while (_ownerThreadCommands.TryDequeue(out var command))
        {
            command();
        }
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
            _driverConfirmedActive = false;
            _lastObserved.Clear();
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

        // S6: each fan gets its own try/catch - one throwing must not stop the others from being
        // restored - and closing the computer is attempted regardless (it is the most reliable way
        // to hand every control back to the BIOS/EC, even for a fan whose RestoreDefault() above
        // just failed).
        foreach (var controller in Controllers)
        {
            try
            {
                controller.RestoreDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore fan {FanId} while closing the hardware monitor.", controller.Id);
            }
        }

        try
        {
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
        var nodes = _computer.Hardware.Select(hw => BuildNode(hw, controllers, _lastObserved)).ToList();
        Controllers = controllers;

        if (!_driverConfirmedActive && HasLiveCpuOrGpuTemperature(nodes))
        {
            // S7: registry/service presence (what IComponentService checks) only means PawnIO is
            // installed, not that LHM actually loaded and is using it this session. A populated
            // CPU/GPU temperature reading is a cheap, reasonably strong proxy that the driver is
            // genuinely active, since those specifically require it.
            _driverConfirmedActive = true;
        }

        Publish(new HardwareSnapshot(ComputeStatus(), null, nodes, DateTimeOffset.UtcNow));
    }

    private static bool HasLiveCpuOrGpuTemperature(IEnumerable<HardwareNode> nodes) =>
        nodes.Any(n =>
            (n.Type is HardwareNodeType.Cpu or HardwareNodeType.Gpu && n.Sensors.Any(s => s.Type == SensorType.Temperature && s.Value is not null)) ||
            HasLiveCpuOrGpuTemperature(n.Children));

    private HardwareStatus ComputeStatus()
    {
        if (!_elevationService.IsElevated)
        {
            return HardwareStatus.NotElevated;
        }

        return _pawnioInstalled && _driverConfirmedActive ? HardwareStatus.Ready : HardwareStatus.DriverMissing;
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

    /// <summary>
    /// B1: in LHM 0.9.6, a fan's software control channel lives on a
    /// <see cref="LibreHardwareMonitor.Hardware.SensorType.Control"/> sensor, not the
    /// <see cref="LibreHardwareMonitor.Hardware.SensorType.Fan"/> (RPM) sensor - see
    /// <c>SuperIOHardware.CreateControlSensors</c>, <c>NvidiaGpu</c>'s per-fan <c>_controls</c>, and
    /// <c>AmdGpu</c>'s <c>_controlSensor</c>. Only the control sensor's <see cref="ISensor.Control"/>
    /// is ever populated. A fan controller is built from each control sensor that has one, paired by
    /// matching <see cref="ISensor.Index"/> with an RPM sensor on the same hardware when one exists
    /// (a bare tachometer with no matching control stays a plain read-only sensor row).
    /// </summary>
    private static HardwareNode BuildNode(
        IHardware hardware,
        List<IFanController> controllers,
        Dictionary<string, (double? Value, DateTimeOffset ObservedUtc)> lastObserved)
    {
        var sensors = new List<SensorReading>();
        var now = DateTimeOffset.UtcNow;

        var fanSensorsByIndex = new Dictionary<int, ISensor>();
        foreach (var s in hardware.Sensors)
        {
            if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Fan)
            {
                fanSensorsByIndex.TryAdd(s.Index, s);
            }
        }

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Control)
            {
                if (sensor.Control is not null)
                {
                    fanSensorsByIndex.TryGetValue(sensor.Index, out var rpmSensor);
                    controllers.Add(new LhmFanController(sensor, rpmSensor));
                }

                // Duty-cycle percent surfaces through the fan card (IFanController.CurrentPercent)
                // instead of the sensors tree, to avoid showing it twice.
                continue;
            }

            var id = sensor.Identifier.ToString();
            var timestamp = StampObservationTime(id, sensor.Value, now, lastObserved);

            sensors.Add(new SensorReading(id, sensor.Name, MapSensorType(sensor.SensorType), sensor.Value, sensor.Min, sensor.Max, timestamp));
        }

        var children = hardware.SubHardware.Select(sub => BuildNode(sub, controllers, lastObserved)).ToList();

        return new HardwareNode(
            hardware.Identifier.ToString(),
            hardware.Name,
            MapNodeType(hardware.HardwareType),
            sensors,
            children);
    }

    /// <summary>
    /// B3: stamps a sensor with "now" only the first time it is seen or when its value has actually
    /// changed; an unchanged value keeps its original timestamp. Without this, every reading was
    /// stamped "now" on every tick regardless of whether it had genuinely refreshed, which made the
    /// rule-3 stale-sensor check mathematically unable to ever fire for a sensor whose value got
    /// stuck at its last successful reading instead of going null.
    /// </summary>
    private static DateTimeOffset StampObservationTime(
        string id,
        double? value,
        DateTimeOffset now,
        Dictionary<string, (double? Value, DateTimeOffset ObservedUtc)> lastObserved)
    {
        if (lastObserved.TryGetValue(id, out var previous) && ValuesEqual(previous.Value, value))
        {
            return previous.ObservedUtc;
        }

        lastObserved[id] = (value, now);
        return now;
    }

    private static bool ValuesEqual(double? a, double? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.Value.Equals(b.Value);
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
        LibreHardwareMonitor.Hardware.SensorType.Data => SensorType.Data,
        LibreHardwareMonitor.Hardware.SensorType.SmallData => SensorType.SmallData,
        LibreHardwareMonitor.Hardware.SensorType.Throughput => SensorType.Throughput,
        LibreHardwareMonitor.Hardware.SensorType.Current => SensorType.Current,
        LibreHardwareMonitor.Hardware.SensorType.Energy => SensorType.Energy,
        LibreHardwareMonitor.Hardware.SensorType.Level => SensorType.Level,
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
