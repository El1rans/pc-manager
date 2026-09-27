using Microsoft.Extensions.Logging;
using PCManager.Core.Settings;

namespace PCManager.Core.Hardware;

/// <summary>
/// Wires <see cref="FanControlEngine"/> (pure decisions) to real fans and settings: on every
/// <see cref="IHardwareService.SnapshotUpdated"/> tick it builds the engine's input from the
/// current settings and temperatures, evaluates, and applies the result to
/// <see cref="IHardwareService.Controllers"/> - all synchronously, on the hardware service's own
/// dedicated thread, so every <see cref="IFanController"/> call stays on the one thread that owns
/// the hardware library (spec 04).
/// </summary>
public sealed class FanControlManager : IDisposable
{
    private readonly IHardwareService _hardwareService;
    private readonly ISettingsStore _settingsStore;
    private readonly FanControlEngine _engine;
    private readonly ILogger<FanControlManager> _logger;
    private readonly Lock _armLock = new();
    private bool _armed;

    public FanControlManager(
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        FanControlEngine engine,
        ILogger<FanControlManager> logger)
    {
        _hardwareService = hardwareService;
        _settingsStore = settingsStore;
        _engine = engine;
        _logger = logger;
        _hardwareService.SnapshotUpdated += OnSnapshot;
    }

    /// <summary>Raised (on the hardware thread) whenever a safety rule needs a banner: overheat
    /// failsafe active, or a set failure disabled software control.</summary>
    public event EventHandler<FanControlAlert>? StatusChanged;

    /// <summary>
    /// Arms software fan control. Per spec 04, the "enabled" setting alone never starts control -
    /// the Hardware page calls this once it has successfully loaded the current sensors and fan
    /// profiles, so control never resumes silently at app startup before the page has shown it is
    /// active.
    /// </summary>
    public void Activate()
    {
        lock (_armLock)
        {
            _armed = true;
        }
    }

    /// <summary>Clears any rule-4 disabled state and (re-)arms control. Called when the user turns
    /// the master switch on - whether for the first time this session or after a set failure had
    /// turned it back off - so a fresh enable never carries over a previous failure's disabled
    /// state.</summary>
    public void Rearm()
    {
        _engine.Reset();
        Activate();
    }

    /// <summary>Rule 5: restores every known fan to default/BIOS control. Called by the app on
    /// exit, crash, system suspend, and session end - independent of the snapshot loop, so it works
    /// even if the hardware thread is not ticking right now.</summary>
    public void RestoreAll()
    {
        foreach (var controller in _hardwareService.Controllers)
        {
            TryRestoreDefault(controller);
        }
    }

    public void Dispose() => _hardwareService.SnapshotUpdated -= OnSnapshot;

    private void OnSnapshot(object? sender, HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        if (controllers.Count == 0)
        {
            return;
        }

        bool armed;
        lock (_armLock)
        {
            armed = _armed;
        }

        var hardwareSettings = _settingsStore.Current.Hardware;
        var input = BuildInput(snapshot, hardwareSettings, controllers, armed);
        var decision = _engine.Evaluate(input);

        foreach (var controller in controllers)
        {
            var target = decision.Targets.GetValueOrDefault(controller.Id, FanTarget.RestoreDefault);
            if (!Apply(controller, target))
            {
                // Rule 4: a set failure disables software control for every fan, everywhere, and
                // shows a critical banner. Stop applying further targets this tick - RestoreAll
                // below already covers them.
                HandleSetFailure(controller, controllers);
                return;
            }
        }

        if (decision.IsOverheatFailsafeActive)
        {
            RaiseStatus(FanControlAlertLevel.Critical,
                $"Overheat failsafe active ({decision.OverheatSensorId}): every fan is at 100% until it cools down.");
        }
    }

    private bool Apply(IFanController controller, FanTarget target)
    {
        try
        {
            if (target.Kind == FanTargetKind.RestoreDefault)
            {
                controller.RestoreDefault();
            }
            else
            {
                controller.SetPercent(target.Percent);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set fan {FanId} ({FanName}).", controller.Id, controller.Name);
            return false;
        }
    }

    private void HandleSetFailure(IFanController failedController, IReadOnlyList<IFanController> controllers)
    {
        _engine.MarkSetFailure();
        RestoreAll();
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
        lock (_armLock)
        {
            _armed = false;
        }

        RaiseStatus(
            FanControlAlertLevel.Critical,
            $"Could not set {failedController.Name}'s speed. Every fan was restored to automatic control and software fan control was turned off.");
    }

    private void TryRestoreDefault(IFanController controller)
    {
        try
        {
            controller.RestoreDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore fan {FanId} ({FanName}) to default control.", controller.Id, controller.Name);
        }
    }

    private void RaiseStatus(FanControlAlertLevel level, string message) =>
        StatusChanged?.Invoke(this, new FanControlAlert(level, message));

    private static FanControlEngineInput BuildInput(
        HardwareSnapshot snapshot,
        HardwareSettings settings,
        IReadOnlyList<IFanController> controllers,
        bool armed)
    {
        var cpuGpuTemperatures = new Dictionary<string, SensorSample>();
        CollectTemperatures(snapshot.Nodes, cpuGpuTemperatures);

        // A curve's source can be any temperature sensor (e.g. a motherboard/VRM sensor), not only
        // a CPU/GPU one, so look those up across every sensor rather than just cpuGpuTemperatures.
        var allTemperatures = snapshot.AllSensors()
            .Where(s => s.Type == SensorType.Temperature)
            .ToDictionary(s => s.Id, s => new SensorSample(s.Value, s.TimestampUtc));

        var profiles = new List<FanProfile>(controllers.Count);
        var fanSourceTemperatures = new Dictionary<string, SensorSample>();

        foreach (var controller in controllers)
        {
            var profile = BuildProfile(controller.Id, settings);
            profiles.Add(profile);

            if (profile is { Mode: FanMode.Curve, SourceSensorId: { } sourceId } &&
                allTemperatures.TryGetValue(sourceId, out var sample))
            {
                fanSourceTemperatures[sourceId] = sample;
            }
        }

        return new FanControlEngineInput(
            SoftwareControlEnabled: armed && settings.FanControlEnabled,
            MinPercent: Math.Max(settings.MinFanPercent, FanControlOptions.LowestAllowedMinPercent),
            FailsafeTemperatureC: settings.FailsafeTemperatureC,
            Profiles: profiles,
            CpuGpuTemperatures: cpuGpuTemperatures,
            FanSourceTemperatures: fanSourceTemperatures);
    }

    private static FanProfile BuildProfile(string fanId, HardwareSettings settings)
    {
        if (!settings.FanProfiles.TryGetValue(fanId, out var saved) || saved.Mode == FanMode.Default)
        {
            return new FanProfile(fanId, FanMode.Default);
        }

        if (saved.Mode == FanMode.Fixed)
        {
            return new FanProfile(fanId, FanMode.Fixed, FixedPercent: saved.FixedPercent);
        }

        var minPercent = Math.Max(settings.MinFanPercent, FanControlOptions.LowestAllowedMinPercent);
        if (saved.SourceSensorId is null ||
            !FanCurve.TryCreate(saved.CurvePoints, minPercent, out var curve, out _))
        {
            // An invalid/incomplete curve is treated the same as a lost sensor - rule 3 - rather
            // than silently falling back to BIOS control (which would look like control is off).
            return new FanProfile(fanId, FanMode.Curve, SourceSensorId: saved.SourceSensorId, Curve: null);
        }

        return new FanProfile(fanId, FanMode.Curve, SourceSensorId: saved.SourceSensorId, Curve: curve);
    }

    private static void CollectTemperatures(IReadOnlyList<HardwareNode> nodes, Dictionary<string, SensorSample> into)
    {
        foreach (var node in nodes)
        {
            if (node.Type is HardwareNodeType.Cpu or HardwareNodeType.Gpu)
            {
                foreach (var sensor in node.Sensors)
                {
                    if (sensor.Type == SensorType.Temperature)
                    {
                        into[sensor.Id] = new SensorSample(sensor.Value, sensor.TimestampUtc);
                    }
                }
            }

            CollectTemperatures(node.Children, into);
        }
    }
}
