using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.App.Controls;
using PCManager.App.Shell;
using PCManager.Core.Components;
using PCManager.Core.Elevation;
using PCManager.Core.Hardware;
using PCManager.Core.Settings;

namespace PCManager.App.Features.Hardware;

public sealed partial class HardwareViewModel : PageViewModelBase, IDisposable
{
    private readonly IHardwareService _hardwareService;
    private readonly FanControlManager _fanControlManager;
    private readonly ISettingsStore _settingsStore;
    private readonly IElevationService _elevationService;
    private readonly ILogger<HardwareViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private bool _loadingToggle;
    private IReadOnlyList<SensorTreeNodeViewModel> _allNodes = [];

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

        var hardware = settingsStore.Current.Hardware;
        _softwareFanControlEnabled = hardware.FanControlEnabled;
        _minFanPercent = Math.Max(hardware.MinFanPercent, FanControlOptions.LowestAllowedMinPercent);
        _failsafeTemperatureC = hardware.FailsafeTemperatureC;

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

    public bool ShowDriverCard => Status == HardwareStatus.DriverMissing;

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
                "PC Manager enforces a minimum speed and an overheat failsafe, but you are " +
                "responsible for the fan curves you choose." + Environment.NewLine + Environment.NewLine +
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

        _allNodes = [.. snapshot.Nodes.Select(n => new SensorTreeNodeViewModel(n))];
        ApplyFilter();
        UpdateFans(snapshot);
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        IEnumerable<SensorTreeNodeViewModel> source = string.IsNullOrEmpty(filter)
            ? _allNodes
            : _allNodes.Where(n => n.Matches(filter));

        SensorNodes.Clear();
        foreach (var node in source)
        {
            SensorNodes.Add(node);
        }
    }

    private void UpdateFans(HardwareSnapshot snapshot)
    {
        var controllers = _hardwareService.Controllers;
        var temperatureSensors = snapshot.AllSensors()
            .Where(s => s.Type == SensorType.Temperature)
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

            var rpmSensor = snapshot.AllSensors().FirstOrDefault(s => s.Id == controller.Id);
            card.UpdateReadings(controller, rpmSensor);
            card.UpdateAvailableTemperatureSensors(temperatureSensors);
            card.UpdateMinFanPercent(MinFanPercent);
        }
    }

    public void Dispose()
    {
        _hardwareService.SnapshotUpdated -= OnSnapshotUpdated;
        _fanControlManager.StatusChanged -= OnFanControlStatusChanged;
    }
}
