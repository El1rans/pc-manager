using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;

namespace Porchlight.App.Features.Cleanup;

/// <summary>"What's using space?" card of the Free up space page: measures a drive or folder off the
/// UI thread and lets the user drill down through the biggest folders and files. Read-only, except
/// that files in the user's own folders can go to the Recycle Bin. See docs/specs/19-disk-insights.md.</summary>
public sealed partial class DiskMapViewModel : ObservableObject, IDisposable
{
    /// <summary>How many rows the drill-down list shows before "Everything else".</summary>
    public const int MaxRows = 30;

    private readonly IDiskSpaceMapper _mapper;
    private readonly IDriveMonitor _driveMonitor;
    private readonly ICleanupPathProvider _paths;
    private readonly IRecycler _recycler;
    private readonly IProcessRunner _processRunner;
    private readonly IFolderPicker _folderPicker;
    private readonly IConfirmationDialog _confirmation;
    private readonly ILogger<DiskMapViewModel> _logger;

    private CancellationTokenSource? _scanCts;
    private DiskNode? _current;
    private string _rootLabel = string.Empty;
    private bool _disposed;

    public DiskMapViewModel(
        IDiskSpaceMapper mapper,
        IDriveMonitor driveMonitor,
        ICleanupPathProvider paths,
        IRecycler recycler,
        IProcessRunner processRunner,
        IFolderPicker folderPicker,
        IConfirmationDialog confirmation,
        ILogger<DiskMapViewModel> logger)
    {
        _mapper = mapper;
        _driveMonitor = driveMonitor;
        _paths = paths;
        _recycler = recycler;
        _processRunner = processRunner;
        _folderPicker = folderPicker;
        _confirmation = confirmation;
        _logger = logger;
        LoadLocations();
    }

    public ObservableCollection<DiskLocationOption> Locations { get; } = [];

    public ObservableCollection<DiskBreadcrumbViewModel> Breadcrumbs { get; } = [];

    public ObservableCollection<DiskRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private DiskLocationOption? _selectedLocation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChooseFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _couldntReadText = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    private void LoadLocations()
    {
        var profile = _paths.UserProfile;
        if (!string.IsNullOrWhiteSpace(profile))
        {
            Locations.Add(new DiskLocationOption("Your files", profile));
        }

        try
        {
            foreach (var drive in _driveMonitor.GetDrives())
            {
                var letter = drive.Name.TrimEnd('\\', '/');
                var label = string.IsNullOrWhiteSpace(drive.Label) ? $"Drive {letter}" : $"{drive.Label} ({letter})";
                Locations.Add(new DiskLocationOption(label, drive.Name));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The drive list is a convenience; "Your files" and "Choose a folder" still work.
            _logger.LogDebug(ex, "Could not list drives for the disk space map.");
        }

        SelectedLocation = Locations.FirstOrDefault();
    }

    // ---------------------------------------------------------------- scanning

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (SelectedLocation is not { } location || _disposed)
        {
            return;
        }

        _scanCts?.Dispose();
        var cts = new CancellationTokenSource();
        _scanCts = cts;

        var path = location.Path;
        IsScanning = true;
        Message = string.Empty;
        ProgressText = "Counting...";
        var progress = new Progress<DiskMapProgress>(p => ProgressText = DiskInsightsTextFormatter.FormatMapProgress(p));

        try
        {
            var root = await _mapper.MapAsync(path, progress, cts.Token);
            _rootLabel = location.Label;
            HasResult = true;
            Show(root);
        }
        catch (OperationCanceledException)
        {
            Message = "Stopped. Nothing was changed.";
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "The folder {Path} no longer exists.", path);
            Message = "Porchlight could not find that folder. Please choose another one.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The disk space scan of {Path} failed.", path);
            Message = "Porchlight could not finish looking at that folder. Please try again.";
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

    private bool CanScan() => !IsScanning && SelectedLocation is not null;

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Stop() => _scanCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanChooseFolder))]
    private void ChooseFolder()
    {
        var picked = _folderPicker.PickFolder(SelectedLocation?.Path);
        if (picked is null)
        {
            return;
        }

        var option = Locations.FirstOrDefault(l => string.Equals(l.Path, picked, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            var name = Path.GetFileName(picked.TrimEnd('\\', '/'));
            option = new DiskLocationOption(string.IsNullOrEmpty(name) ? picked : name, picked);
            Locations.Add(option);
        }

        SelectedLocation = option;
    }

    private bool CanChooseFolder() => !IsScanning;

    // ---------------------------------------------------------------- drilling down

    private void Show(DiskNode node)
    {
        _current = node;

        Breadcrumbs.Clear();
        var chain = new List<DiskNode>();
        for (var step = node; step is not null; step = step.Parent)
        {
            chain.Add(step);
        }

        chain.Reverse();
        for (var i = 0; i < chain.Count; i++)
        {
            var label = i == 0 ? _rootLabel : chain[i].Name;
            Breadcrumbs.Add(new DiskBreadcrumbViewModel(label, chain[i], isFirst: i == 0, isCurrent: i == chain.Count - 1));
        }

        SummaryText = DiskInsightsTextFormatter.FormatSummary(node.TotalBytes, node.FileCount);
        CouldntReadText = DiskInsightsTextFormatter.FormatCouldntRead(node.UnreadableFolders);

        var candidates = node.Children
            .Where(child => child.TotalBytes > 0)
            .Select(child => (Bytes: child.TotalBytes, Row: DiskRowViewModel.ForFolder(child, node)))
            .Concat(node.Files.Select(file => (Bytes: file.Bytes, Row: DiskRowViewModel.ForFile(file, node))))
            .OrderByDescending(item => item.Bytes)
            .ToList();

        Rows.Clear();
        foreach (var item in candidates.Take(MaxRows))
        {
            Rows.Add(item.Row);
        }

        var rest = node.TotalBytes - candidates.Take(MaxRows).Sum(item => item.Bytes);
        if (rest > 0 && candidates.Count > 0)
        {
            Rows.Add(DiskRowViewModel.ForOther(rest, node));
        }
    }

    [RelayCommand]
    private void OpenRow(DiskRowViewModel? row)
    {
        if (row is { Folder: { } folder })
        {
            Show(folder);
        }
    }

    [RelayCommand]
    private void GoTo(DiskBreadcrumbViewModel? crumb)
    {
        if (crumb is not null)
        {
            Show(crumb.Node);
        }
    }

    // ---------------------------------------------------------------- actions on a row

    [RelayCommand]
    private void ShowInFolder(DiskRowViewModel? row)
    {
        if (row is null || !row.CanShowInFolder)
        {
            return;
        }

        try
        {
            // A folder is opened; a file is selected inside its folder.
            if (row.IsFolder)
            {
                _processRunner.StartDetached("explorer.exe", [row.Path]);
            }
            else
            {
                _processRunner.StartDetached("explorer.exe", ["/select,", row.Path]);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not open Explorer for {Path}.", row.Path);
            Message = "Porchlight could not open that folder.";
        }
    }

    [RelayCommand]
    private async Task MoveToRecycleBinAsync(DiskRowViewModel? row)
    {
        // Only files the map flagged as recyclable (ordinary files in the user's own folders).
        if (row is not { CanRecycle: true, File: { } file, Owner: { } owner } || IsScanning)
        {
            return;
        }

        var confirmed = _confirmation.Confirm(
            "Move to the Recycle Bin?",
            $"Move \"{row.Name}\" ({row.SizeText}) to the Recycle Bin?\n\nYou can get it back from the Recycle Bin until you empty it.");
        if (!confirmed)
        {
            return;
        }

        Message = string.Empty;
        var moved = await Task.Run(() => _recycler.MoveToRecycleBin(file.FullPath));
        if (moved)
        {
            owner.RemoveFile(file);
            if (_current is { } current)
            {
                Show(current);
            }
        }
        else
        {
            Message = $"Porchlight could not move \"{row.Name}\" to the Recycle Bin. It may be open in another program.";
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCts?.Cancel();
    }
}
