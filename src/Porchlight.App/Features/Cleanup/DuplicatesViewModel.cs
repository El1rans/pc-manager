using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;

namespace Porchlight.App.Features.Cleanup;

/// <summary>"Duplicate files" card of the Free up space page: finds identical files in the user's
/// own folders and moves the copies the user ticks to the Recycle Bin, always leaving one copy of
/// each. See docs/specs/19-disk-insights.md.</summary>
public sealed partial class DuplicatesViewModel : ObservableObject, IDisposable
{
    private readonly IDuplicateFinder _finder;
    private readonly IDuplicateRemover _remover;
    private readonly ICleanupPathProvider _paths;
    private readonly IProcessRunner _processRunner;
    private readonly IConfirmationDialog _confirmation;
    private readonly ILogger<DuplicatesViewModel> _logger;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _removeCts;
    private List<DuplicateGroup> _coreGroups = [];
    private bool _disposed;

    public DuplicatesViewModel(
        IDuplicateFinder finder,
        IDuplicateRemover remover,
        ICleanupPathProvider paths,
        IProcessRunner processRunner,
        IConfirmationDialog confirmation,
        ILogger<DuplicatesViewModel> logger)
    {
        _finder = finder;
        _remover = remover;
        _paths = paths;
        _processRunner = processRunner;
        _confirmation = confirmation;
        _logger = logger;
    }

    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedCommand))]
    private bool _isRemoving;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedCommand))]
    private bool _hasSelection;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _selectedText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    // ---------------------------------------------------------------- scanning

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (_disposed)
        {
            return;
        }

        _scanCts?.Dispose();
        var cts = new CancellationTokenSource();
        _scanCts = cts;

        IsScanning = true;
        Message = string.Empty;
        ResultText = string.Empty;
        ProgressText = "Looking through your files...";
        var progress = new Progress<DuplicateProgress>(p => ProgressText = DiskInsightsTextFormatter.FormatDuplicateProgress(p));

        try
        {
            var options = new DuplicateSearchOptions(_paths.PersonalFolders);
            var groups = await _finder.FindAsync(options, progress, cts.Token);
            SetGroups(groups.ToList());
            HasScanned = true;
        }
        catch (OperationCanceledException)
        {
            Message = "Stopped. Nothing was changed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The duplicate file scan failed.");
            Message = "Porchlight could not finish looking for duplicates. Please try again.";
        }
        finally
        {
            IsScanning = false;
            ProgressText = string.Empty;
            if (ReferenceEquals(_scanCts, cts))
            {
                _scanCts = null;
            }

            cts.Dispose();
        }
    }

    private bool CanScan() => !IsScanning && !IsRemoving;

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Stop() => _scanCts?.Cancel();

    private void SetGroups(List<DuplicateGroup> groups)
    {
        _coreGroups = groups;
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(new DuplicateGroupViewModel(group, OnSelectionChanged, OnLastCopyRefused));
        }

        SummaryText = groups.Count == 0
            ? "No duplicate files found."
            : $"Found {groups.Count} {(groups.Count == 1 ? "set" : "sets")} of identical files. Keeping one copy of each would free {ByteFormatter.FormatBytes(groups.Sum(g => g.WastedBytes))}.";
        OnSelectionChanged();
    }

    // ---------------------------------------------------------------- choosing copies

    private void OnLastCopyRefused() =>
        Message = "Keep at least one copy of each file. Untick a copy first.";

    private void OnSelectionChanged()
    {
        var selected = Groups.SelectMany(group => group.SelectedFiles).ToList();
        HasSelection = selected.Count > 0;
        var bytes = Groups.Sum(group => group.Group.FileBytes * group.SelectedFiles.Count());
        SelectedText = selected.Count == 0
            ? string.Empty
            : $"Selected: {selected.Count} {(selected.Count == 1 ? "file" : "files")}, {ByteFormatter.FormatBytes(bytes)}";
        if (selected.Count > 0)
        {
            Message = string.Empty;
        }
    }

    [RelayCommand]
    private void KeepNewestInEveryGroup()
    {
        foreach (var group in Groups)
        {
            group.ApplyKeepNewest();
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var group in Groups)
        {
            group.ClearSelection();
        }
    }

    [RelayCommand]
    private void ShowInFolder(DuplicateFileRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            _processRunner.StartDetached("explorer.exe", ["/select,", row.File.FullPath]);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not open Explorer for {Path}.", row.File.FullPath);
            Message = "Porchlight could not open that folder.";
        }
    }

    // ---------------------------------------------------------------- removing

    /// <summary>True while the Recycle Bin move runs, so closing the app asks first.</summary>
    public bool IsBusyWithWork => IsRemoving;

    [RelayCommand(CanExecute = nameof(CanMoveSelected))]
    private async Task MoveSelectedAsync()
    {
        var selected = Groups.SelectMany(group => group.SelectedFiles).Select(row => row.File.FullPath).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var bytes = Groups.Sum(group => group.Group.FileBytes * group.SelectedFiles.Count());
        var noun = selected.Count == 1 ? "file" : "files";
        if (!_confirmation.Confirm(
                "Move to the Recycle Bin?",
                $"Move {selected.Count} {noun} ({ByteFormatter.FormatBytes(bytes)}) to the Recycle Bin?\n\n" +
                "One copy of each file stays where it is. You can get the moved files back from the Recycle Bin until you empty it."))
        {
            return;
        }

        _removeCts?.Dispose();
        var cts = new CancellationTokenSource();
        _removeCts = cts;
        IsRemoving = true;
        Message = string.Empty;
        ResultText = string.Empty;

        try
        {
            var result = await _remover.RemoveAsync(_coreGroups, selected, cts.Token);
            ResultText = DiskInsightsTextFormatter.FormatRemoveResult(result);
            Prune(result.RemovedPaths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Moving duplicate files to the Recycle Bin failed.");
            Message = "Porchlight could not move those files. Please try again.";
        }
        finally
        {
            IsRemoving = false;
            if (ReferenceEquals(_removeCts, cts))
            {
                _removeCts = null;
            }

            cts.Dispose();
        }
    }

    private bool CanMoveSelected() => HasSelection && !IsScanning && !IsRemoving;

    /// <summary>Drops the removed copies from the list; a set with fewer than two copies left is no
    /// longer a duplicate and disappears.</summary>
    private void Prune(IReadOnlyList<string> removedPaths)
    {
        var removed = new HashSet<string>(removedPaths, StringComparer.OrdinalIgnoreCase);
        var updated = new List<DuplicateGroup>();
        foreach (var group in _coreGroups)
        {
            var left = group.Files.Where(file => !removed.Contains(file.FullPath)).ToList();
            if (left.Count >= 2)
            {
                updated.Add(group with { Files = left });
            }
        }

        SetGroups(updated);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCts?.Cancel();
        _removeCts?.Cancel();
    }
}
