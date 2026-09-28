using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Hardware;

/// <summary>
/// One fan card: current RPM/percent, mode selector, fixed slider (floor enforced), and curve
/// editor. Every user edit is saved immediately through <see cref="ISettingsStore.Update"/> -
/// there is no separate "Apply" step, matching the rest of the app's settings.
/// </summary>
public sealed partial class FanCardViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<FanCardViewModel> _logger;
    private bool _loading;

    /// <summary>Whether this fan's RPM sensor has ever reported a value greater than zero - see
    /// <see cref="UpdateVisibility"/>. Cached so <see cref="UpdateHideUnusedSensorsSetting"/> can
    /// recompute <see cref="IsVisible"/> from just the setting, without a fresh reading.</summary>
    private bool _everReportedRpm = true;

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

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>Spec 04 addendum: the user's custom name for this fan, persisted keyed by
    /// <see cref="FanId"/> (never an index). Empty means "use the hardware-reported name" - see
    /// <see cref="DisplayName"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(HasCustomName))]
    private string _customName = string.Empty;

    [ObservableProperty]
    private bool _isEditingName;

    [ObservableProperty]
    private string _nameEditText = string.Empty;

    /// <summary>Fan display text for the RPM reading - "Stopped (idle)" instead of "0 RPM" for a
    /// GPU fan reading 0 (spec 04 addendum: GPUs commonly run a legitimate 0-RPM idle mode).</summary>
    [ObservableProperty]
    private string _rpmDisplayText = "-";

    public FanCardViewModel(string fanId, string name, HardwareNodeType nodeType, ISettingsStore settingsStore, ILogger<FanCardViewModel> logger)
    {
        FanId = fanId;
        Name = name;
        NodeType = nodeType;
        _settingsStore = settingsStore;
        _logger = logger;

        CurvePoints.CollectionChanged += (_, _) =>
        {
            SaveProfile();
            OnPropertyChanged(nameof(CanAddCurvePoint));
            AddCurvePointCommand.NotifyCanExecuteChanged();
        };
        LoadFromSettings();
    }

    public string FanId { get; }

    /// <summary>Hardware-reported name - always shown as secondary/subtitle text once a custom
    /// name is set (spec 04 addendum), regardless of <see cref="DisplayName"/>.</summary>
    public string Name { get; }

    public HardwareNodeType NodeType { get; }

    /// <summary>The custom name when set, otherwise <see cref="Name"/> - the same fallback rule as
    /// <see cref="FanNaming.ResolveDisplayName"/> (which is what every other place a fan name is
    /// shown - Sensors tab RPM rows, the "Hottest fan" summary tile - uses via
    /// <c>HardwareViewModel</c>'s settings-backed lookup, kept in agreement with this property).</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? Name : CustomName;

    public bool HasCustomName => !string.IsNullOrWhiteSpace(CustomName);

    public ObservableCollection<EditableCurvePoint> CurvePoints { get; } = [];

    public IReadOnlyList<FanMode> Modes { get; } = [FanMode.Default, FanMode.Fixed, FanMode.Curve];

    public IReadOnlyList<SensorOption> TemperatureSensorOptions { get; private set; } = [];

    /// <summary>Called every tick with the live controller/RPM readings for this fan.</summary>
    public void UpdateReadings(IFanController? controller, SensorReading? rpmSensor)
    {
        CurrentPercent = controller?.CurrentPercent;
        CurrentRpm = rpmSensor?.Value;

        // Spec 04 addendum: a GPU fan reading 0 RPM is commonly a legitimate "idle" fan mode, not
        // a stalled/unconnected fan the way it would be on a motherboard header - say so plainly
        // instead of the ordinary "0 RPM", which reads as an error.
        RpmDisplayText = NodeType == HardwareNodeType.Gpu && (CurrentRpm is null or 0)
            ? "Stopped (idle)"
            : CurrentRpm is { } rpm
                ? $"{rpm:0} RPM"
                : "- RPM";
    }

    public void UpdateAvailableTemperatureSensors(IReadOnlyList<SensorOption> options) =>
        TemperatureSensorOptions = options;

    /// <summary>Fans polish addendum: recomputes <see cref="IsVisible"/> from this tick's RPM
    /// history and the "Hide unused sensors" setting. A GPU fan is never hidden (see
    /// <see cref="NodeType"/>'s remarks); a fan that has ever reported RPM greater than zero stays
    /// visible forever after, even if it later idles at 0.</summary>
    public void UpdateVisibility(bool hideUnusedSensors, bool everReportedRpm)
    {
        _everReportedRpm = everReportedRpm;
        RecomputeVisibility(hideUnusedSensors);
    }

    /// <summary>Called when the "Hide unused sensors" toggle itself changes, so visibility updates
    /// immediately without waiting for the next hardware tick - reuses the RPM history already
    /// cached by the most recent <see cref="UpdateVisibility"/> call.</summary>
    public void UpdateHideUnusedSensorsSetting(bool hideUnusedSensors) => RecomputeVisibility(hideUnusedSensors);

    private void RecomputeVisibility(bool hideUnusedSensors) =>
        IsVisible = NodeType == HardwareNodeType.Gpu || !hideUnusedSensors || _everReportedRpm;

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

    /// <summary>Spec 04 addendum: begins inline renaming, pre-filling the edit box with the
    /// current custom name (empty if none is set, so the box starts blank rather than pre-filled
    /// with the hardware name the user would just be re-typing).</summary>
    [RelayCommand]
    private void BeginRenaming()
    {
        NameEditText = CustomName;
        IsEditingName = true;
    }

    [RelayCommand]
    private void CommitRename()
    {
        var trimmed = NameEditText.Trim();
        IsEditingName = false;
        if (trimmed == CustomName)
        {
            return;
        }

        CustomName = trimmed;
        _settingsStore.Update(s =>
        {
            if (string.IsNullOrWhiteSpace(CustomName))
            {
                s.Hardware.FanDisplayNames.Remove(FanId);
            }
            else
            {
                s.Hardware.FanDisplayNames[FanId] = CustomName;
            }
        });
    }

    [RelayCommand]
    private void CancelRenaming() => IsEditingName = false;

    partial void OnModeChanged(FanMode value)
    {
        if (!_loading)
        {
            _logger.LogInformation("Fan {FanId} ({FanName}) mode changed to {Mode}.", FanId, DisplayName, value);
        }

        SaveProfile();
    }

    partial void OnFixedPercentChanged(double value)
    {
        if (!_loading)
        {
            _logger.LogInformation("Fan {FanId} ({FanName}) fixed target changed to {Percent}%.", FanId, DisplayName, value);
        }

        SaveProfile();
    }

    partial void OnSourceSensorIdChanged(string? value) => SaveProfile();

    private void LoadFromSettings()
    {
        _loading = true;
        try
        {
            var settings = _settingsStore.Current.Hardware;
            if (settings.FanDisplayNames.TryGetValue(FanId, out var customName))
            {
                CustomName = customName;
            }

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
