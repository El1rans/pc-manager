using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Hardware;
using PCManager.Core.Settings;

namespace PCManager.App.Features.Hardware;

/// <summary>
/// One fan card: current RPM/percent, mode selector, fixed slider (floor enforced), and curve
/// editor. Every user edit is saved immediately through <see cref="ISettingsStore.Update"/> -
/// there is no separate "Apply" step, matching the rest of the app's settings.
/// </summary>
public sealed partial class FanCardViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private bool _loading;

    [ObservableProperty]
    private double? _currentRpm;

    [ObservableProperty]
    private double? _currentPercent;

    [ObservableProperty]
    private FanMode _mode = FanMode.Default;

    [ObservableProperty]
    private double _fixedPercent = FanControlOptions.DefaultMinPercent;

    [ObservableProperty]
    private string? _sourceSensorId;

    [ObservableProperty]
    private int _minFanPercent = FanControlOptions.DefaultMinPercent;

    [ObservableProperty]
    private string? _curveError;

    [ObservableProperty]
    private bool _canEditMode;

    [ObservableProperty]
    private string _modeEditDisabledReason = "Turn on software fan control to change this.";

    public FanCardViewModel(string fanId, string name, ISettingsStore settingsStore)
    {
        FanId = fanId;
        Name = name;
        _settingsStore = settingsStore;

        CurvePoints.CollectionChanged += (_, _) =>
        {
            SaveProfile();
            OnPropertyChanged(nameof(CanAddCurvePoint));
            AddCurvePointCommand.NotifyCanExecuteChanged();
        };
        LoadFromSettings();
    }

    public string FanId { get; }

    public string Name { get; }

    public ObservableCollection<EditableCurvePoint> CurvePoints { get; } = [];

    public IReadOnlyList<FanMode> Modes { get; } = [FanMode.Default, FanMode.Fixed, FanMode.Curve];

    public IReadOnlyList<SensorOption> TemperatureSensorOptions { get; private set; } = [];

    /// <summary>Called every tick with the live controller/RPM readings for this fan.</summary>
    public void UpdateReadings(IFanController? controller, SensorReading? rpmSensor)
    {
        CurrentPercent = controller?.CurrentPercent;
        CurrentRpm = rpmSensor?.Value;
    }

    public void UpdateAvailableTemperatureSensors(IReadOnlyList<SensorOption> options) =>
        TemperatureSensorOptions = options;

    /// <summary>Recommended follow-up from the safety review: the mode selector should not look
    /// editable when nothing would actually happen if the user changed it - profiles can still be
    /// viewed either way.</summary>
    public void UpdateModeEditable(bool canEdit)
    {
        CanEditMode = canEdit;
    }

    public void UpdateMinFanPercent(int minFanPercent)
    {
        MinFanPercent = minFanPercent;
        if (FixedPercent < minFanPercent)
        {
            FixedPercent = minFanPercent;
        }

        // The floor moving can turn a previously-valid curve invalid (a point now below the new
        // minimum) or vice versa - re-check without touching the points or re-persisting them.
        CurveError = Mode == FanMode.Curve && !TryValidateCurve(out var error) ? error : null;
    }

    public bool CanAddCurvePoint =>
        CurvePoints.Count < FanControlOptions.MaxCurvePoints &&
        (CurvePoints.Count == 0 || CurvePoints.Max(p => p.TemperatureC) < 99);

    /// <summary>Bound to the curve editor's drag-completed callback, so a point drag is persisted
    /// once the user releases it rather than on every intermediate mouse-move.</summary>
    [RelayCommand]
    private void CommitCurve() => SaveProfile();

    [RelayCommand(CanExecute = nameof(CanAddCurvePoint))]
    private void AddCurvePoint()
    {
        if (!CanAddCurvePoint)
        {
            return;
        }

        var last = CurvePoints.OrderBy(p => p.TemperatureC).LastOrDefault();
        var temp = last is null ? 40 : Math.Min(last.TemperatureC + 10, 99);
        var percent = last is null ? MinFanPercent : Math.Min(last.Percent + 10, 100);
        CurvePoints.Add(new EditableCurvePoint(temp, percent));
        SaveProfile();
    }

    [RelayCommand]
    private void RemoveCurvePoint(EditableCurvePoint? point)
    {
        if (point is null || CurvePoints.Count <= FanControlOptions.MinCurvePoints)
        {
            return;
        }

        CurvePoints.Remove(point);
        SaveProfile();
    }

    /// <summary>Called by the curve editor after a drag ends (and by the point-count commands
    /// above) to validate and persist the curve.</summary>
    public void SaveProfile()
    {
        if (_loading)
        {
            return;
        }

        _settingsStore.Update(s =>
        {
            var profile = new FanProfileSettings
            {
                Mode = Mode,
                FixedPercent = FixedPercent,
                SourceSensorId = SourceSensorId,
                CurvePoints = [.. CurvePoints.OrderBy(p => p.TemperatureC).Select(p => p.ToPoint())],
            };
            s.Hardware.FanProfiles[FanId] = profile;
        });

        CurveError = Mode == FanMode.Curve && !TryValidateCurve(out var error) ? error : null;
    }

    private bool TryValidateCurve(out string? error)
    {
        var points = CurvePoints.OrderBy(p => p.TemperatureC).Select(p => p.ToPoint()).ToList();
        return FanCurve.TryCreate(points, MinFanPercent, out _, out error);
    }

    partial void OnModeChanged(FanMode value) => SaveProfile();

    partial void OnFixedPercentChanged(double value) => SaveProfile();

    partial void OnSourceSensorIdChanged(string? value) => SaveProfile();

    private void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var settings = _settingsStore.Current.Hardware;
            if (settings.FanProfiles.TryGetValue(FanId, out var saved))
            {
                Mode = saved.Mode;
                FixedPercent = saved.FixedPercent > 0 ? saved.FixedPercent : settings.MinFanPercent;
                SourceSensorId = saved.SourceSensorId;
                foreach (var point in saved.CurvePoints)
                {
                    CurvePoints.Add(new EditableCurvePoint(point));
                }
            }

            if (CurvePoints.Count == 0)
            {
                CurvePoints.Add(new EditableCurvePoint(40, settings.MinFanPercent));
                CurvePoints.Add(new EditableCurvePoint(50, Math.Min(settings.MinFanPercent + 20, 100)));
                CurvePoints.Add(new EditableCurvePoint(70, 80));
                CurvePoints.Add(new EditableCurvePoint(85, 100));
            }
        }
        finally
        {
            _loading = false;
        }
    }
}
