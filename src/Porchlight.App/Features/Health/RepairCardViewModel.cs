using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>
/// The Windows repair card: SFC first, then (only if SFC could not fix everything) DISM. Neither run
/// can be cancelled once started - see <see cref="IWindowsRepairService"/>.
/// </summary>
public sealed partial class RepairCardViewModel : ObservableObject
{
    private const int LogCharacterLimit = 100_000;
    private const int LogTrimTarget = 75_000;

    public const string DurationWarning =
        "This can take 10 to 30 minutes. Keep Porchlight open and leave your PC on. It can't be stopped once it has started.";

    private readonly IWindowsRepairService _service;
    private readonly IElevationService _elevation;
    private readonly ILogger<RepairCardViewModel> _logger;
    private readonly StringBuilder _log = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCheckCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunDeeperRepairCommand))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunDeeperRepairCommand))]
    private bool _deeperRepairOffered;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private string? _stepText;

    [ObservableProperty]
    private string? _resultText;

    [ObservableProperty]
    private HealthSeverity _resultSeverity;

    [ObservableProperty]
    private string _logText = string.Empty;

    public RepairCardViewModel(
        IWindowsRepairService service, IElevationService elevation, ILogger<RepairCardViewModel> logger)
    {
        _service = service;
        _elevation = elevation;
        _logger = logger;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Bound from XAML, needs an instance property.")]
    public string Warning => DurationWarning;

    public bool IsElevated => _elevation.IsElevated;

    public bool HasResult => !string.IsNullOrEmpty(ResultText);

    public string ResultGlyph => HealthGlyphs.For(ResultSeverity);

    partial void OnResultTextChanged(string? value) => OnPropertyChanged(nameof(HasResult));

    partial void OnResultSeverityChanged(HealthSeverity value) => OnPropertyChanged(nameof(ResultGlyph));

    private bool CanStart() => IsElevated && !IsRunning;

    private bool CanRunDeeper() => IsElevated && !IsRunning && DeeperRepairOffered;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartCheckAsync()
    {
        BeginRun("Checking Windows files...");
        DeeperRepairOffered = false;
        try
        {
            var outcome = await _service.RunSfcAsync(CreateProgress(), CancellationToken.None);
            ResultText = RepairOutcomeDescriber.Describe(outcome);
            ResultSeverity = outcome switch
            {
                SfcOutcome.NoProblems or SfcOutcome.Repaired => HealthSeverity.Ok,
                SfcOutcome.Unknown => HealthSeverity.Neutral,
                _ => HealthSeverity.Warning,
            };
            DeeperRepairOffered = RepairOutcomeDescriber.OffersDism(outcome);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Windows file check failed unexpectedly.");
            ResultText = "The check stopped unexpectedly. Restart your PC and try again.";
            ResultSeverity = HealthSeverity.Warning;
        }
        finally
        {
            EndRun();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunDeeper))]
    private async Task RunDeeperRepairAsync()
    {
        BeginRun("Running the deeper repair...");
        try
        {
            var outcome = await _service.RunDismAsync(CreateProgress(), CancellationToken.None);
            ResultText = RepairOutcomeDescriber.Describe(outcome);
            ResultSeverity = outcome == DismOutcome.Succeeded ? HealthSeverity.Ok : HealthSeverity.Warning;
            DeeperRepairOffered = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "DISM repair failed unexpectedly.");
            ResultText = "The deeper repair stopped unexpectedly. Restart your PC and try again.";
            ResultSeverity = HealthSeverity.Warning;
        }
        finally
        {
            EndRun();
        }
    }

    /// <summary>Created on the UI thread, so callbacks arrive on the UI thread.</summary>
    private Progress<RepairProgress> CreateProgress() => new(OnProgress);

    private void OnProgress(RepairProgress update)
    {
        if (update.Percent is { } percent)
        {
            IsIndeterminate = false;
            Percent = percent;
        }

        if (update.Line is { } line)
        {
            _log.AppendLine(line);
            if (_log.Length > LogCharacterLimit)
            {
                _log.Remove(0, _log.Length - LogTrimTarget);
            }

            LogText = _log.ToString();
        }
    }

    private void BeginRun(string step)
    {
        IsRunning = true;
        IsIndeterminate = true;
        Percent = 0;
        StepText = step;
        ResultText = null;
    }

    private void EndRun()
    {
        IsRunning = false;
        IsIndeterminate = false;
        StepText = null;
    }
}
