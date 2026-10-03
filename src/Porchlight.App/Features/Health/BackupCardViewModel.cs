using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Backup;
using Porchlight.Core.Processes;

namespace Porchlight.App.Features.Health;

/// <summary>The Backup card: is anything backing up the person's files, and when did it last run?
/// Read-only - the buttons only open Windows' own screens.</summary>
public sealed partial class BackupCardViewModel(
    IBackupStatusService service,
    IProcessRunner processRunner,
    TimeProvider timeProvider,
    ILogger<BackupCardViewModel> logger) : HealthCardViewModelBase
{
    private const string ControlExe = "control.exe";
    private const string ExplorerExe = "explorer.exe";
    private const string FileHistoryApplet = "Microsoft.FileHistory";
    private const string BackupSettingsUri = "ms-settings:backup";

    private string? _oneDriveExePath;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _headlineText = string.Empty;

    [ObservableProperty]
    private string? _nudgeText;

    [ObservableProperty]
    private string? _otherToolsText;

    [ObservableProperty]
    private string _glyph = HealthGlyphs.Unknown;

    [ObservableProperty]
    private HealthSeverity _severity;

    [ObservableProperty]
    private bool _canTurnOnFileHistory;

    [ObservableProperty]
    private bool _canOpenOneDrive;

    [ObservableProperty]
    private string? _launchError;

    public ObservableCollection<string> DetailLines { get; } = [];

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        try
        {
            var result = await service.GetAsync(cancellationToken);
            DetailLines.Clear();
            LaunchError = null;
            if (!result.Succeeded || result.Value is null)
            {
                HasResult = false;
                CanTurnOnFileHistory = false;
                CanOpenOneDrive = false;
                SetFailure(result.Error);
                return;
            }

            ErrorText = null;
            var assessment = BackupVerdictEvaluator.Evaluate(result.Value, timeProvider.GetLocalNow());
            Severity = assessment.Verdict switch
            {
                BackupVerdict.Good => HealthSeverity.Ok,
                BackupVerdict.Warning => HealthSeverity.Warning,
                _ => HealthSeverity.Problem,
            };
            Glyph = HealthGlyphs.For(Severity);
            HeadlineText = assessment.Headline;
            NudgeText = assessment.Nudge;
            OtherToolsText = assessment.OtherToolsNote;
            foreach (var line in assessment.Lines)
            {
                DetailLines.Add(line);
            }

            CanTurnOnFileHistory = assessment.OfferFileHistory;
            _oneDriveExePath = result.Value.OneDrive?.ExePath;
            CanOpenOneDrive = _oneDriveExePath is not null;
            HasResult = true;
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void TurnOnFileHistory() =>
        Launch(Path.Combine(Environment.SystemDirectory, ControlExe), ["/name", FileHistoryApplet],
            "Couldn't open File History. Search for \"File History\" in the Start menu.");

    [RelayCommand]
    private void OpenBackupSettings() =>
        Launch(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), ExplorerExe), [BackupSettingsUri],
            "Couldn't open backup settings. Open Settings and search for \"backup\".");

    [RelayCommand]
    private void OpenOneDrive()
    {
        if (_oneDriveExePath is { } path)
        {
            Launch(path, [], "Couldn't open OneDrive. Click its cloud icon near the clock instead.");
        }
    }

    private void Launch(string fileName, IReadOnlyList<string> arguments, string failureText)
    {
        try
        {
            LaunchError = null;
            processRunner.StartDetached(fileName, arguments);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(ex, "Could not open {FileName}.", Path.GetFileName(fileName));
            LaunchError = failureText;
        }
    }
}
