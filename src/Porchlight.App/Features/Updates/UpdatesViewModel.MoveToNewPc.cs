using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Winget;

namespace Porchlight.App.Features.Updates;

/// <summary>"Move to a new PC": save the list of installed apps, and install apps from such a list.
/// See docs/specs/16-checkup-report.md, part B.</summary>
public sealed partial class UpdatesViewModel
{
    private const string AppListFilter = "App list (*.json)|*.json";
    private const string DefaultAppListFileName = "My apps.json";

    private string? _pendingImportPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusyWithWork))]
    private bool _isMovingApps;

    /// <summary>The validated apps from the file the user picked, waiting for their confirmation.
    /// Null when no import is waiting.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportConfirmationVisible))]
    [NotifyPropertyChangedFor(nameof(ImportConfirmationMessage))]
    [NotifyPropertyChangedFor(nameof(PendingImportAppIds))]
    private WingetExportParseResult? _pendingImport;

    /// <summary>One-line result or problem for the "Move to a new PC" area.</summary>
    [ObservableProperty]
    private string? _moveToNewPcMessage;

    public bool IsImportConfirmationVisible => PendingImport is not null;

    public string ImportConfirmationMessage => PendingImport is { } import
        ? $"Install {import.Apps.Count} app{(import.Apps.Count == 1 ? string.Empty : "s")} from this list? Apps that are not available are skipped."
        : string.Empty;

    /// <summary>The app ids to show in the confirmation list.</summary>
    public IReadOnlyList<string> PendingImportAppIds => PendingImport?.Apps.Select(a => a.Id).ToList() ?? [];

    partial void OnIsBusyChanged(bool value)
    {
        SaveAppListCommand.NotifyCanExecuteChanged();
        ChooseAppListCommand.NotifyCanExecuteChanged();
        ConfirmImportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMoveApps))]
    private async Task SaveAppListAsync()
    {
        var path = _fileDialogs.PickSaveFile(
            "Save my app list", DefaultAppListFileName, AppListFilter,
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        if (path is null)
        {
            return;
        }

        MoveToNewPcMessage = null;
        IsBusy = true;
        IsMovingApps = true;
        try
        {
            AppendLog("Saving the list of installed apps...");
            var result = await _wingetClient
                .ExportAsync(path, new Progress<string>(AppendLog), new Progress<string>(text => ProgressLine = text), CancellationToken.None)
                .ConfigureAwait(true);

            MoveToNewPcMessage = result.ExitCode == 0 && File.Exists(path)
                ? $"Saved your app list to {Path.GetFileName(path)}. Copy it to your new PC."
                : "Couldn't save the app list. Open the log below to see why.";
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MoveToNewPcMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure saving the app list.");
            AppendLog("ERROR: " + ex.Message);
            MoveToNewPcMessage = "Couldn't save the app list. Open the log below to see why.";
        }
        finally
        {
            FlushLog();
            ProgressLine = null;
            IsMovingApps = false;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveApps))]
    private async Task ChooseAppListAsync()
    {
        var path = _fileDialogs.PickOpenFile("Choose an app list", AppListFilter);
        if (path is null)
        {
            return;
        }

        MoveToNewPcMessage = null;
        PendingImport = null;
        _pendingImportPath = null;

        var result = await WingetExportFile.LoadAsync(path, _logger, CancellationToken.None).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            MoveToNewPcMessage = result.Error;
            return;
        }

        _pendingImportPath = path;
        PendingImport = result;
    }

    [RelayCommand]
    private void CancelImport()
    {
        PendingImport = null;
        _pendingImportPath = null;
    }

    [RelayCommand(CanExecute = nameof(CanConfirmImport))]
    private async Task ConfirmImportAsync()
    {
        if (PendingImport is not { } confirmed || _pendingImportPath is not { } path)
        {
            return;
        }

        PendingImport = null;
        _pendingImportPath = null;
        IsBusy = true;
        IsMovingApps = true;
        try
        {
            // The file could have been swapped after the user confirmed the list: install only if
            // it still lists exactly the apps that were shown.
            var current = await WingetExportFile.LoadAsync(path, _logger, CancellationToken.None).ConfigureAwait(true);
            if (!current.IsSuccess || !current.Apps.Select(a => a.Id).SequenceEqual(confirmed.Apps.Select(a => a.Id)))
            {
                MoveToNewPcMessage = "The app list changed after you chose it. Nothing was installed. Please choose it again.";
                return;
            }

            AppendLog($"Installing {current.Apps.Count} apps from the list...");
            // CancellationToken.None: never kill winget mid-install - see IWingetClient.ImportAsync.
            var result = await _wingetClient
                .ImportAsync(path, new Progress<string>(AppendLog), new Progress<string>(text => ProgressLine = text), CancellationToken.None)
                .ConfigureAwait(true);

            var summary = result.ExitCode == 0
                ? "Finished installing the apps from the list."
                : "Finished, but some apps could not be installed. Open the log below to see which.";
            AppendLog(summary);
            MoveToNewPcMessage = summary;
        }
        catch (WingetNotFoundException ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MoveToNewPcMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure installing apps from a list.");
            AppendLog("ERROR: " + ex.Message);
            MoveToNewPcMessage = "Something went wrong installing the apps. Open the log below to see why.";
        }
        finally
        {
            FlushLog();
            ProgressLine = null;
            IsMovingApps = false;
            IsBusy = false;
        }

        await RefreshAsync(quiet: true).ConfigureAwait(true);
    }

    private bool CanMoveApps() => !IsBusy;

    private bool CanConfirmImport() => !IsBusy && PendingImport is not null;

    partial void OnPendingImportChanged(WingetExportParseResult? value) => ConfirmImportCommand.NotifyCanExecuteChanged();
}
