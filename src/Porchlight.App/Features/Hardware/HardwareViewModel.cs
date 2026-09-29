using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Controls;
using Porchlight.App.Shell;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;
using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Hardware;

public sealed partial class HardwareViewModel : PageViewModelBase, IDisposable
{
    private readonly IHardwareService _hardwareService;
    private readonly FanControlManager _fanControlManager;
    private readonly ISettingsStore _settingsStore;
    private readonly IElevationService _elevationService;
    private readonly IFanControlConflictDetector _conflictDetector;
    private readonly ILogger<HardwareViewModel> _logger;
    private readonly ILogger<FanCardViewModel> _fanCardLogger;
    private readonly Dispatcher _dispatcher;
    private readonly UnusedSensorTracker _unusedSensorTracker = new();
    private bool _loadingToggle;
    private bool _disposed;
    private volatile bool _viewActive;
    private HardwareSnapshot? _pendingSnapshot;

    [ObservableProperty]
    private HardwareStatus _status = HardwareStatus.NotElevated;

    [ObservableProperty]
    private string? _criticalMessage;

    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>Spec 10: "Hide unused sensors" toggle, on by default, persisted via
    /// <see cref="ISettingsStore"/>.</summary>
    [ObservableProperty]
    private bool _hideUnusedSensors;

    [ObservableProperty]
    private bool _softwareFanControlEnabled;

    [ObservableProperty]
    private int _minFanPercent = FanControlOptions.DefaultMinPercent;

    [ObservableProperty]
    private double _failsafeTemperatureC = FanControlOptions.DefaultFailsafeTemperatureC;

    /// <summary>Spec 04 addendum: display names of every known fan-control tool currently detected
    /// running alongside Porchlight - see <see cref="IFanControlConflictDetector"/>. Empty when
    /// none are detected (the common case).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowConflictWarning))]
    [NotifyPropertyChangedFor(nameof(ConflictWarningMessage))]
    private IReadOnlyList<string> _detectedConflictingSoftware = [];

    public HardwareViewModel(
        IHardwareService hardwareService,
        FanControlManager fanControlManager,
        ISettingsStore settingsStore,
        IElevationService elevationService,
        IComponentCardViewModelFactory componentCardFactory,
        IFanControlConflictDetector conflictDetector,
        ILogger<HardwareViewModel> logger,
        ILogger<FanCardViewModel> fanCardLogger)
    {
        _hardwareService = hardwareService;
        _fanControlManager = fanControlManager;
        _settingsStore = settingsStore;
        _elevationService = elevationService;
        _conflictDetector = conflictDetector;
        _logger = logger;
        _fanCardLogger = fanCardLogger;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        PawnIoCard = componentCardFactory.Create(ComponentIds.PawnIo);
        PawnIoCard.PropertyChanged += OnPawnIoCardPropertyChanged;

        var hardware = settingsStore.Current.Hardware;
        _hideUnusedSensors = hardware.HideUnusedSensors;
        _softwareFanControlEnabled = hardware.FanControlEnabled;
        _minFanPercent = Math.Max(hardware.MinFanPercent, FanControlOptions.LowestAllowedMinPercent);
        // S2: clamp on load too - a value only ever clamped in the property-changed handler leaves
        // a value read straight from settings.json (e.g. hand-edited, or from an older version with
        // a different default) unclamped for the whole first tick.
        _failsafeTemperatureC = Math.Clamp(
            hardware.FailsafeTemperatureC,
            FanControlOptions.LowestAllowedFailsafeTemperatureC,
            FanControlOptions.HighestAllowedFailsafeTemperatureC);

        if (_fanControlManager.StaleActivityMarkerDetected)
        {
            // S8: Porchlight cannot itself hand a fan back to BIOS control after an unclean
            // shutdown - only a PC restart re-initializes the EC/SuperIO - so say so plainly.
            CriticalMessage = "Fans may still be under software control from the last session. Restart your PC to return them to BIOS control.";
        }

        _hardwareService.SnapshotUpdated += OnSnapshotUpdated;
        _fanControlManager.StatusChanged += OnFanControlStatusChanged;

        ApplySnapshot(_hardwareService.Latest);
    }

    public override string Title => "Hardware";

    public override string Glyph => "";

    public override int Order => 1;

    public override PageCategory Category => PageCategory.Hardware;

    public override string TabTitle => "Sensors & fans";

    public ComponentCardViewModel PawnIoCard { get; }

    public bool IsElevated => _elevationService.IsElevated;

    public bool ShowAdminBanner => !IsElevated;

    /// <summary>
    /// S7: shown whenever PawnIO is not installed, regardless of Porchlight's own elevation -
    /// winget elevates its own installer via UAC, so there is no need to already be elevated to
    /// start that. The admin banner above still covers the (different) fact that most sensors and
    /// all fan control also need <em>Porchlight itself</em> to be elevated.
    /// </summary>
    public bool ShowDriverCard => PawnIoCard.Status.State is not (ComponentState.Installed or ComponentState.Running);

    /// <summary>B1: some hardware (most laptop ECs) exposes no software-controllable fan channel to
    /// Windows at all - shown once fully initialized and still empty, so it isn't confused with
    /// "still loading" or "needs admin/driver".</summary>
    public bool ShowNoControllableFansMessage => Status == HardwareStatus.Ready && Fans.Count == 0;

    public bool CanToggleSoftwareFanControl => Status == HardwareStatus.Ready;

    public string SoftwareFanControlDisabledReason => Status switch
    {
        HardwareStatus.NotElevated => "Restart as administrator to use software fan control.",
        HardwareStatus.DriverMissing => "Install the PawnIO driver above to use software fan control.",
        HardwareStatus.Error => "Fan control is unavailable while the hardware monitor has an error.",
        _ => string.Empty,
    };

    /// <summary>Spec 04 addendum: whether the Fans tab's conflicting-software warning banner
    /// should show.</summary>
    public bool ShowConflictWarning => DetectedConflictingSoftware.Count > 0;

    /// <summary>Spec 04 addendum: the Fans tab's conflicting-software warning banner text.</summary>
    public string ConflictWarningMessage => DetectedConflictingSoftware.Count == 0
        ? string.Empty
        : $"{string.Join(", ", DetectedConflictingSoftware)} " +
          (DetectedConflictingSoftware.Count == 1 ? "is" : "are") +
          " also controlling your fans. Porchlight's settings may be overridden. Close/disable it to use Porchlight fan control.";

    /// <summary>"At a glance" summary strip (spec 10): up to 4 tiles, merged in place every tick.</summary>
    public ObservableCollection<HardwareSummaryTileViewModel> SummaryTiles { get; } = [];

    /// <summary>One card per hardware device, merged in place every tick (S10) - see
    /// <see cref="HardwareCardViewModel.UpdateFrom"/>. Membership never changes on a filter alone;
    /// only each card/section/row's <c>IsVisible</c> does, so expansion state and the Expander
    /// containers are never discarded just because the filter text changed.</summary>
    public ObservableCollection<HardwareCardViewModel> Cards { get; } = [];

    public ObservableCollection<FanCardViewModel> Fans { get; } = [];

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Per spec 04: the persisted "enabled" setting alone never starts software fan control -
        // it only resumes once the Hardware page has actually loaded (this call), so there is never
        // silent control at app startup before the user has seen this page is active.
        _fanControlManager.Activate();
        _ = RefreshConflictDetectionCommand.ExecuteAsync(null);
        return Task.CompletedTask;
    }

    /// <summary>Spec 04 addendum: called from the view's code-behind whenever the Fans tab becomes
    /// the selected tab, so a tool started/closed after the page first loaded is still noticed
    /// without polling every snapshot tick (process/service enumeration is comparatively
    /// expensive).</summary>
    public void OnFansTabSelected() => _ = RefreshConflictDetectionCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task RefreshConflictDetectionAsync()
    {
        try
        {
            DetectedConflictingSoftware = await Task.Run(_conflictDetector.DetectConflicts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check for conflicting fan-control software.");
        }
    }

    [RelayCommand]
    private void ResetMinMax() => _hardwareService.ResetMinMax();

    [RelayCommand]
    private void RestoreAllFans()
    {
        SetToggleWithoutSideEffects(false);
        _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
        _fanControlManager.RestoreAll();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnHideUnusedSensorsChanged(bool value)
    {
        _settingsStore.Update(s => s.Hardware.HideUnusedSensors = value);
        ApplyFilter();
        foreach (var fan in Fans)
        {
            fan.UpdateHideUnusedSensorsSetting(value);
        }
    }

    partial void OnMinFanPercentChanged(int value)
    {
        var clamped = Math.Max(value, FanControlOptions.LowestAllowedMinPercent);
        if (clamped != value)
        {
            MinFanPercent = clamped;
            return;
        }

        _settingsStore.Update(s => s.Hardware.MinFanPercent = clamped);
        foreach (var fan in Fans)
        {
            fan.UpdateMinFanPercent(clamped);
        }
    }

    partial void OnFailsafeTemperatureCChanged(double value)
    {
        var clamped = Math.Clamp(
            value, FanControlOptions.LowestAllowedFailsafeTemperatureC, FanControlOptions.HighestAllowedFailsafeTemperatureC);
        if (Math.Abs(clamped - value) > 0.001)
        {
            FailsafeTemperatureC = clamped;
            return;
        }

        _settingsStore.Update(s => s.Hardware.FailsafeTemperatureC = clamped);
    }

    partial void OnSoftwareFanControlEnabledChanged(bool value)
    {
        if (_loadingToggle)
        {
            return;
        }

        if (!value)
        {
            _settingsStore.Update(s => s.Hardware.FanControlEnabled = false);
            _fanControlManager.RestoreAll();
            return;
        }

        if (Status != HardwareStatus.Ready)
        {
            SetToggleWithoutSideEffects(false);
            return;
        }

        if (!_settingsStore.Current.Hardware.FanControlWarningConfirmed)
        {
            var result = MessageBox.Show(
                "Software fan control can overheat and damage your hardware if set up wrong. " +
                "Porchlight enforces a minimum speed and an overheat failsafe, but you are " +
                "responsible for the fan curves you choose." + Environment.NewLine + Environment.NewLine +
                "If Porchlight is forced to close, crashes, or the PC loses power while a fan is " +
                "under software control, that fan stays at its last commanded speed until you " +
                "restart the PC - relaunching Porchlight alone will not hand it back to BIOS " +
                "control." + Environment.NewLine + Environment.NewLine +
                "Turn on software fan control?",
                "Enable software fan control",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                SetToggleWithoutSideEffects(false);
                return;
            }

            _settingsStore.Update(s => s.Hardware.FanControlWarningConfirmed = true);
        }

        _settingsStore.Update(s => s.Hardware.FanControlEnabled = true);
        _fanControlManager.Rearm();
        CriticalMessage = null;
    }

    private void SetToggleWithoutSideEffects(bool value)
    {
        _loadingToggle = true;
        SoftwareFanControlEnabled = value;
        _loadingToggle = false;
    }

    private void OnPawnIoCardPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ComponentCardViewModel.Status))
        {
            OnPropertyChanged(nameof(ShowDriverCard));
        }
    }

    /// <summary>Called by the view whenever it becomes visible/hidden (page selected or not, window
    /// shown or hidden to tray). While inactive, snapshot ticks are only remembered, not applied -
    /// this page is pure display; fan control runs off <see cref="IHardwareService.SnapshotUpdated"/>
    /// in <see cref="FanControlManager"/> directly and never depends on it. Becoming active again
    /// applies the latest snapshot immediately.</summary>
    public void SetViewActive(bool active)
    {
        _viewActive = active;
        if (active)
        {
            ApplyPendingSnapshot();
        }
    }

    /// <summary>Called by the view when the main window's state changes, so a restore from minimized
    /// catches up immediately instead of on the next tick.</summary>
    public void OnWindowStateChanged() => ApplyPendingSnapshot();

    private void OnSnapshotUpdated(object? sender, HardwareSnapshot snapshot)
    {
        _pendingSnapshot = snapshot;
        if (_viewActive)
        {
            _dispatcher.InvokeAsync(ApplyPendingSnapshot);
        }
    }

    /// <summary>Applies the newest not-yet-applied snapshot, if any, and only while the page is
    /// shown and the main window is not minimized. UI thread only.</summary>
    private void ApplyPendingSnapshot()
    {
        if (_disposed || !_viewActive || Application.Current?.MainWindow?.WindowState == WindowState.Minimized)
        {
            return;
        }

        var snapshot = _pendingSnapshot;
        if (snapshot is null)
        {
            return;
        }

        _pendingSnapshot = null;
        ApplySnapshot(snapshot);
    }

    private void OnFanControlStatusChanged(object? sender, FanControlAlert alert) =>
        _dispatcher.InvokeAsync(() =>
        {
            if (alert.Level == FanControlAlertLevel.Critical)
            {
                CriticalMessage = alert.Message;
                SetToggleWithoutSideEffects(_settingsStore.Current.Hardware.FanControlEnabled);
            }
        });

    private void ApplySnapshot(HardwareSnapshot snapshot)
    {
        Status = snapshot.Status;
        OnPropertyChanged(nameof(ShowAdminBanner));
        OnPropertyChanged(nameof(ShowDriverCard));
        OnPropertyChanged(nameof(CanToggleSoftwareFanControl));
        OnPropertyChanged(nameof(SoftwareFanControlDisabledReason));

        var fanDisplayNameOverrides = BuildFanDisplayNameOverridesByRpmSensorId();
        MergeCards(snapshot.Nodes, fanDisplayNameOverrides);
        ApplyFilter();
        _unusedSensorTracker.PruneTo(snapshot.AllSensors().Select(s => s.Id).ToHashSet());
        MergeSummaryTiles(HardwareSummarySelector.Build(snapshot, FailsafeTemperatureC, fanDisplayNameOverrides));
        UpdateFans(snapshot);
        OnPropertyChanged(nameof(ShowNoControllableFansMessage));
    }

    /// <summary>Spec 04 addendum: maps every controller's custom display name (keyed by
    /// <see cref="IFanController.Id"/> in settings) to its paired RPM sensor id, since that is the
    /// id the Sensors tab's rows and the "Hottest fan" summary tile key by. A controller with no
    /// RPM sensor (rare) or no custom name is simply absent from the result.</summary>
    private Dictionary<string, string> BuildFanDisplayNameOverridesByRpmSensorId()
    {
        var customNames = _settingsStore.Current.Hardware.FanDisplayNames;
        var overrides = new Dictionary<string, string>();
        if (customNames.Count == 0)
        {
            return overrides;
        }

        foreach (var controller in _hardwareService.Controllers)
        {
            if (controller.RpmSensorId is { } rpmId &&
                customNames.TryGetValue(controller.Id, out var custom) &&
                !string.IsNullOrWhiteSpace(custom))
            {
                overrides[rpmId] = custom;
            }
        }

        return overrides;
    }

    /// <summary>Sensors-tab card order (spec 10): "CPU, GPU, Motherboard, Memory, Storage,
    /// Network" - anything else sorts after Network, in the order the snapshot itself reports it.</summary>
    private static int CardOrderRank(HardwareNodeType type) => type switch
    {
        HardwareNodeType.Cpu => 0,
        HardwareNodeType.Gpu => 1,
        HardwareNodeType.Motherboard => 2,
        HardwareNodeType.Memory => 3,
        HardwareNodeType.Storage => 4,
        HardwareNodeType.Network => 5,
        _ => 6,
    };

    /// <summary>S10: updates existing card view models in place (and lets each merge its own
    /// sections/sensors) instead of clearing and rebuilding <see cref="Cards"/> every tick.</summary>
    private void MergeCards(IReadOnlyList<HardwareNode> nodes, IReadOnlyDictionary<string, string> fanDisplayNameOverrides)
    {
        var byId = new Dictionary<string, HardwareCardViewModel>(Cards.Count);
        foreach (var existing in Cards)
        {
            byId[existing.Id] = existing;
        }

        var ordered = nodes
            .Select((node, index) => (Node: node, Index: index))
            .OrderBy(t => CardOrderRank(t.Node.Type))
            .ThenBy(t => t.Index)
            .ToList();

        var seen = new HashSet<string>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var node = ordered[i].Node;
            seen.Add(node.Id);

            if (byId.TryGetValue(node.Id, out var existingVm))
            {
                existingVm.UpdateFrom(node, _unusedSensorTracker, fanDisplayNameOverrides);
                var currentIndex = Cards.IndexOf(existingVm);
                if (currentIndex != i && i < Cards.Count)
                {
                    Cards.Move(currentIndex, i);
                }
            }
            else
            {
                var created = new HardwareCardViewModel(node, _unusedSensorTracker, fanDisplayNameOverrides);
                if (i < Cards.Count)
                {
                    Cards.Insert(i, created);
                }
                else
                {
                    Cards.Add(created);
                }
            }
        }

        for (var i = Cards.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(Cards[i].Id))
            {
                Cards.RemoveAt(i);
            }
        }
    }

    /// <summary>Merges the summary strip in place by title so its tile order stays stable tick to
    /// tick even though a tile can appear/disappear (e.g. no GPU temperature sensor found).</summary>
    private void MergeSummaryTiles(IReadOnlyList<HardwareSummaryTile> tiles)
    {
        var byTitle = new Dictionary<string, HardwareSummaryTileViewModel>(SummaryTiles.Count);
        foreach (var existing in SummaryTiles)
        {
            byTitle[existing.Title] = existing;
        }

        var seen = new HashSet<string>(tiles.Count);
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            seen.Add(tile.Title);

            if (byTitle.TryGetValue(tile.Title, out var existingVm))
            {
                existingVm.UpdateFrom(tile);
                var currentIndex = SummaryTiles.IndexOf(existingVm);
                if (currentIndex != i && i < SummaryTiles.Count)
                {
                    SummaryTiles.Move(currentIndex, i);
                }
            }
            else
            {
                var created = new HardwareSummaryTileViewModel();
                created.UpdateFrom(tile);
                if (i < SummaryTiles.Count)
                {
                    SummaryTiles.Insert(i, created);
                }
                else
                {
                    SummaryTiles.Add(created);
                }
            }
        }

        for (var i = SummaryTiles.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(SummaryTiles[i].Title))
            {
                SummaryTiles.RemoveAt(i);
            }
        }
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        foreach (var card in Cards)
        {
            card.ApplyFilter(filter, HideUnusedSensors);
        }
    }

    private void UpdateFans(HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        var allSensors = snapshot.AllSensors().ToList();
        var sensorsById = new Dictionary<string, SensorReading>(allSensors.Count);
        foreach (var sensor in allSensors)
        {
            sensorsById[sensor.Id] = sensor;
        }

        // S1: a sensor with inverted scale (e.g. Intel's per-core "Distance to TjMax") must never be
        // offered as a curve source - selecting one would make the curve react backwards.
        var temperatureSensors = allSensors
            .Where(s => s.Type == SensorType.Temperature && !SensorNaming.IsInvertedTemperature(s.Name))
            .Select(s => new SensorOption(s.Id, s.Name))
            .ToList();

        var currentIds = controllers.Select(c => c.Id).ToHashSet();
        for (var i = Fans.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Fans[i].FanId))
            {
                Fans.RemoveAt(i);
            }
        }

        foreach (var controller in controllers)
        {
            var card = Fans.FirstOrDefault(f => f.FanId == controller.Id);
            if (card is null)
            {
                card = new FanCardViewModel(controller.Id, controller.Name, controller.NodeType, _settingsStore, _fanCardLogger);
                Fans.Add(card);
            }

            // B1: the RPM reading now lives on a separate sensor from the control channel itself -
            // look it up by the id the controller pairs it with, not by the controller's own id.
            var rpmSensor = controller.RpmSensorId is { } rpmId
                ? sensorsById.GetValueOrDefault(rpmId)
                : null;
            card.UpdateReadings(controller, rpmSensor);
            card.UpdateAvailableTemperatureSensors(temperatureSensors);
            card.UpdateMinFanPercent(MinFanPercent);
            card.UpdateModeEditable(CanToggleSoftwareFanControl && SoftwareFanControlEnabled);

            // Fans polish addendum: hide a fan header that has never once reported RPM > 0 (an
            // unconnected motherboard header) when "Hide unused sensors" is on - reusing the same
            // tracker/setting the Sensors tab's "Hide unused sensors" toggle already uses, so the
            // two never disagree about what "unused" means. A GPU fan is never hidden regardless
            // of its RPM history (GPUs commonly run a legitimate 0-RPM idle mode), and a fan with
            // no matching RPM sensor at all is never hidden either (nothing to judge history from).
            var everReportedRpm = rpmSensor is null || _unusedSensorTracker.IsEverUsed(rpmSensor.Id);
            card.UpdateVisibility(HideUnusedSensors, everReportedRpm);
        }
    }

    /// <summary>Idempotent: page view models are disposed twice on host shutdown (see
    /// <see cref="Shell.PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/>). Disposes
    /// <see cref="PawnIoCard"/>, which this view model created and so owns.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hardwareService.SnapshotUpdated -= OnSnapshotUpdated;
        _fanControlManager.StatusChanged -= OnFanControlStatusChanged;
        PawnIoCard.PropertyChanged -= OnPawnIoCardPropertyChanged;
        PawnIoCard.Dispose();
    }
}
