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
    private readonly ILogger<HardwareViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private bool _loadingToggle;
    private bool _disposed;

    [ObservableProperty]
    private HardwareStatus _status = HardwareStatus.NotElevated;

    [ObservableProperty]
    private string? _criticalMessage;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _softwareFanControlEnabled;

    [ObservableProperty]
    private int _minFanPercent = FanControlOptions.DefaultMinPercent;

    [ObservableProperty]
    private double _failsafeTemperatureC = FanControlOptions.DefaultFailsafeTemperatureC;

    public HardwareViewModel(
        IHardwareService hardwareService,
        FanControlManager fanControlManager,
        ISettingsStore settingsStore,
        IElevationService elevationService,
        IComponentCardViewModelFactory componentCardFactory,
        ILogger<HardwareViewModel> logger)
    {
        _hardwareService = hardwareService;
        _fanControlManager = fanControlManager;
        _settingsStore = settingsStore;
        _elevationService = elevationService;
        _logger = logger;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        PawnIoCard = componentCardFactory.Create(ComponentIds.PawnIo);
        PawnIoCard.PropertyChanged += OnPawnIoCardPropertyChanged;

        var hardware = settingsStore.Current.Hardware;
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

    public override int Order => 2;

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

    /// <summary>Root hardware nodes, merged in place every tick (S10) - see
    /// <see cref="SensorTreeNodeViewModel.UpdateFrom"/>. Membership never changes on a filter alone;
    /// only <see cref="SensorTreeNodeViewModel.IsVisible"/> does, so expansion state and the
    /// underlying <c>TreeViewItem</c> containers are never discarded just because the filter text
    /// changed.</summary>
    public ObservableCollection<SensorTreeNodeViewModel> SensorNodes { get; } = [];

    public ObservableCollection<FanCardViewModel> Fans { get; } = [];

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Per spec 04: the persisted "enabled" setting alone never starts software fan control -
        // it only resumes once the Hardware page has actually loaded (this call), so there is never
        // silent control at app startup before the user has seen this page is active.
        _fanControlManager.Activate();
        return Task.CompletedTask;
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

    private void OnSnapshotUpdated(object? sender, HardwareSnapshot snapshot) =>
        _dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));

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

        MergeSensorNodes(snapshot.Nodes);
        ApplyFilter();
        UpdateFans(snapshot);
        OnPropertyChanged(nameof(ShowNoControllableFansMessage));
    }

    /// <summary>S10: updates existing root node view models in place (and lets each merge its own
    /// children/sensors) instead of clearing and rebuilding <see cref="SensorNodes"/> every tick.</summary>
    private void MergeSensorNodes(IReadOnlyList<HardwareNode> nodes)
    {
        var byId = new Dictionary<string, SensorTreeNodeViewModel>(SensorNodes.Count);
        foreach (var existing in SensorNodes)
        {
            byId[existing.Id] = existing;
        }

        var seen = new HashSet<string>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            seen.Add(node.Id);

            if (byId.TryGetValue(node.Id, out var existingVm))
            {
                existingVm.UpdateFrom(node);
                var currentIndex = SensorNodes.IndexOf(existingVm);
                if (currentIndex != i && i < SensorNodes.Count)
                {
                    SensorNodes.Move(currentIndex, i);
                }
            }
            else
            {
                var created = new SensorTreeNodeViewModel(node);
                if (i < SensorNodes.Count)
                {
                    SensorNodes.Insert(i, created);
                }
                else
                {
                    SensorNodes.Add(created);
                }
            }
        }

        for (var i = SensorNodes.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(SensorNodes[i].Id))
            {
                SensorNodes.RemoveAt(i);
            }
        }
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        foreach (var node in SensorNodes)
        {
            node.ApplyFilter(filter);
        }
    }

    private void UpdateFans(HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        var allSensors = snapshot.AllSensors().ToList();

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
                card = new FanCardViewModel(controller.Id, controller.Name, _settingsStore);
                Fans.Add(card);
            }

            // B1: the RPM reading now lives on a separate sensor from the control channel itself -
            // look it up by the id the controller pairs it with, not by the controller's own id.
            var rpmSensor = controller.RpmSensorId is { } rpmId
                ? allSensors.FirstOrDefault(s => s.Id == rpmId)
                : null;
            card.UpdateReadings(controller, rpmSensor);
            card.UpdateAvailableTemperatureSensors(temperatureSensors);
            card.UpdateMinFanPercent(MinFanPercent);
            card.UpdateModeEditable(CanToggleSoftwareFanControl && SoftwareFanControlEnabled);
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
