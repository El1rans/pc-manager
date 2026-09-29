using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Dashboard;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Elevation;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Processes;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Cleanup;

/// <summary>"Free up space" page: cleans safe, regenerable junk in one click and only ever
/// suggests (never auto-deletes) big personal files and large apps. See
/// docs/specs/12-disk-cleanup.md.</summary>
public sealed partial class CleanupViewModel : PageViewModelBase, IBusyGuard, IDisposable
{
    private const string StorageSettingsUri = "ms-settings:storagesense";
    private const string InstalledAppsUri = "ms-settings:appsfeatures";
    private const int TopAppCount = 25;

    private readonly ICleanupPathProvider _paths;
    private readonly ICleanupCatalog _catalog;
    private readonly ICleanupScanner _scanner;
    private readonly ICleanupRunner _runner;
    private readonly ILargeFileFinder _fileFinder;
    private readonly IRecycler _recycler;
    private readonly IInstalledAppsReader _appsReader;
    private readonly IAppUninstaller _uninstaller;
    private readonly IDriveMonitor _driveMonitor;
    private readonly IElevationService _elevation;
    private readonly IProcessRunner _processRunner;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IConfirmationDialog _confirmation;
    private readonly ISettingsStore _settings;
    private readonly ILogger<CleanupViewModel> _logger;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _cleanCts;
    private List<CleanupAppRowViewModel> _allApps = [];
    private bool _restoringSelection;
    private bool _disposed;

    public CleanupViewModel(
        ICleanupPathProvider paths,
        ICleanupCatalog catalog,
        ICleanupScanner scanner,
        ICleanupRunner runner,
        ILargeFileFinder fileFinder,
        IRecycler recycler,
        IInstalledAppsReader appsReader,
        IAppUninstaller uninstaller,
        IDriveMonitor driveMonitor,
        IElevationService elevation,
        IProcessRunner processRunner,
        IUrlLauncher urlLauncher,
        IConfirmationDialog confirmation,
        ISettingsStore settings,
        DiskMapViewModel diskMap,
        DuplicatesViewModel duplicates,
        ILogger<CleanupViewModel> logger)
    {
        _paths = paths;
        _catalog = catalog;
        _scanner = scanner;
        _runner = runner;
        _fileFinder = fileFinder;
        _recycler = recycler;
        _appsReader = appsReader;
        _uninstaller = uninstaller;
        _driveMonitor = driveMonitor;
        _elevation = elevation;
        _processRunner = processRunner;
        _urlLauncher = urlLauncher;
        _confirmation = confirmation;
        _settings = settings;
        DiskMap = diskMap;
        Duplicates = duplicates;
        _logger = logger;
        FreedSoFarText = CleanupTextFormatter.FormatFreedSoFar(settings.Current.Cleanup.TotalBytesFreed);
    }

    public override string Title => "Free up space";

    public override string Glyph => "";

    public override int Order => 3;

    public override PageCategory Category => PageCategory.TuneUp;

    public ObservableCollection<CleanupCategoryRowViewModel> Categories { get; } = [];

    public ObservableCollection<CleanupFileRowViewModel> BigFiles { get; } = [];

    public ObservableCollection<CleanupFileRowViewModel> OldDownloads { get; } = [];

    public ObservableCollection<CleanupAppRowViewModel> Apps { get; } = [];

    /// <summary>The "What's using space?" card (disk space map).</summary>
    public DiskMapViewModel DiskMap { get; }

    /// <summary>The "Duplicate files" card.</summary>
    public DuplicatesViewModel Duplicates { get; }

    [ObservableProperty]
    private DriveRowViewModel? _systemDrive;

    [ObservableProperty]
    private string _freedSoFarText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isCleaning;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
    private bool _hasSelection;

    [ObservableProperty]
    private string _selectedTotalText = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private string _filesMessage = string.Empty;

    [ObservableProperty]
    private string _appsMessage = string.Empty;

    [ObservableProperty]
    private bool _showAdminBanner;

    [ObservableProperty]
    private bool _canShowAllApps;

    /// <inheritdoc/>
    public bool IsBusyWithWork => IsCleaning || Duplicates.IsBusyWithWork;

    /// <inheritdoc/>
    public string BusyMessage =>
        "Porchlight is still cleaning up. If you close it now, the cleanup stops part-way (nothing is harmed). Close anyway?";

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) =>
        IsCleaning ? Task.CompletedTask : RunScanAsync(clearResult: true);

    // ---------------------------------------------------------------- scanning

    [RelayCommand(CanExecute = nameof(CanScan))]
    private Task ScanAsync() => RunScanAsync(clearResult: true);

    private bool CanScan() => !IsScanning && !IsCleaning;

    private async Task RunScanAsync(bool clearResult)
    {
        if (_disposed)
        {
            return;
        }

        // A newer visit or "Scan again" replaces an in-flight scan.
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _scanCts = cts;
        var token = cts.Token;

        IsScanning = true;
        if (clearResult)
        {
            ResultText = string.Empty;
        }

        try
        {
            RefreshDrive();
            var isElevated = _elevation.IsElevated;
            var categories = await Task.Run(_catalog.GetCategories, token);
            BuildCategoryRows(categories, isElevated);
            ShowAdminBanner = !isElevated && categories.Any(c => c.RequiresAdmin || c.Roots.Any(r => r.RequiresAdmin));

            var progress = new Progress<CleanupProgress>(OnScanProgress);
            var toScan = Categories.Where(row => row.IsEnabled).Select(row => row.Category).ToList();
            var scan = await _scanner.ScanAsync(toScan, progress, token);
            foreach (var result in scan.Categories)
            {
                Categories.FirstOrDefault(row => row.Category.Id == result.Id)?.ApplyScan(result);
            }

            HasScanned = true;
            UpdateSelectionSummary();

            await Task.WhenAll(LoadBigFilesAsync(token), LoadOldDownloadsAsync(token), LoadAppsAsync(token));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scan, or the app is closing; nothing to show.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "The disk cleanup scan failed.");
            ResultText = "Porchlight could not finish checking your disk. Please try again.";
        }
        finally
        {
            if (ReferenceEquals(_scanCts, cts))
            {
                IsScanning = false;
            }
        }
    }

    private void OnScanProgress(CleanupProgress progress)
    {
        var row = Categories.FirstOrDefault(r => r.Category.Id == progress.Category);
        if (row is { IsEnabled: true } && !HasScanned)
        {
            row.SizeText = ByteFormatter.FormatBytes(progress.Bytes);
        }
    }

    private void BuildCategoryRows(IReadOnlyList<CleanupCategory> categories, bool isElevated)
    {
        var settings = _settings.Current.Cleanup;
        var deselected = settings.DeselectedCategories.ToHashSet(StringComparer.Ordinal);
        var chosen = settings.SelectedCategories.ToHashSet(StringComparer.Ordinal);

        _restoringSelection = true;
        try
        {
            Categories.Clear();
            foreach (var category in categories)
            {
                var name = category.Id.ToString();
                var selected = category.IsSelectedByDefault ? !deselected.Contains(name) : chosen.Contains(name);
                var enabled = !category.RequiresAdmin || isElevated;
                Categories.Add(new CleanupCategoryRowViewModel(category, selected, enabled, OnCategorySelectionChanged));
            }
        }
        finally
        {
            _restoringSelection = false;
        }

        HasScanned = false;
    }

    private void OnCategorySelectionChanged(CleanupCategoryRowViewModel row)
    {
        UpdateSelectionSummary();
        if (_restoringSelection)
        {
            return;
        }

        // Remember only the user's own choices, by category name.
        var name = row.Category.Id.ToString();
        _settings.Update(s =>
        {
            s.Cleanup.DeselectedCategories.Remove(name);
            s.Cleanup.SelectedCategories.Remove(name);
            if (row.Category.IsSelectedByDefault && !row.IsSelected)
            {
                s.Cleanup.DeselectedCategories.Add(name);
            }
            else if (!row.Category.IsSelectedByDefault && row.IsSelected)
            {
                s.Cleanup.SelectedCategories.Add(name);
            }
        });
    }

    private void UpdateSelectionSummary()
    {
        var selected = Categories.Where(row => row.IsEnabled && row.IsSelected).ToList();
        HasSelection = selected.Count > 0;
        SelectedTotalText = HasScanned && selected.Count > 0
            ? $"Selected: {ByteFormatter.FormatBytes(selected.Sum(row => row.Bytes))} can be freed"
            : string.Empty;
    }

    private void RefreshDrive()
    {
        try
        {
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            var drives = _driveMonitor.GetDrives();
            var drive = drives.FirstOrDefault(d => string.Equals(d.Name, systemRoot, StringComparison.OrdinalIgnoreCase))
                ?? (drives.Count > 0 ? drives[0] : null);
            if (drive is null)
            {
                return;
            }

            if (SystemDrive is null)
            {
                SystemDrive = new DriveRowViewModel(drive);
            }
            else
            {
                SystemDrive.Apply(drive);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The header bar is a nicety; the page works without it.
            _logger.LogDebug(ex, "Could not read the system drive.");
        }
    }

    // ---------------------------------------------------------------- cleaning

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task CleanAsync()
    {
        var rows = Categories.Where(row => row.IsEnabled && row.IsSelected).ToList();
        if (rows.Count == 0)
        {
            return;
        }

        if (rows.Any(row => row.Category.IsRecycleBin) &&
            !_confirmation.Confirm(
                "Empty the Recycle Bin?",
                "This permanently deletes everything in the Recycle Bin. You cannot get these files back.\n\nEmpty it?"))
        {
            return;
        }

        _scanCts?.Cancel();
        _cleanCts = new CancellationTokenSource();
        IsCleaning = true;
        ResultText = string.Empty;
        ProgressText = "Cleaning up...";

        try
        {
            var progress = new Progress<CleanupProgress>(p =>
                ProgressText = $"Cleaning up... {ByteFormatter.FormatBytes(p.Bytes)} freed so far");
            // Navigating away does not cancel a clean; only "Stop" does (see IsBusyWithWork).
            var result = await _runner.CleanAsync(rows.Select(row => row.Category).ToList(), progress, _cleanCts.Token);

            ResultText = CleanupTextFormatter.FormatResult(result);
            if (result.BytesFreed > 0)
            {
                _settings.Update(s =>
                {
                    s.Cleanup.TotalBytesFreed += result.BytesFreed;
                    s.Cleanup.LastCleanedUtc = DateTime.UtcNow;
                });
                FreedSoFarText = CleanupTextFormatter.FormatFreedSoFar(_settings.Current.Cleanup.TotalBytesFreed);
            }
        }
        finally
        {
            IsCleaning = false;
            ProgressText = string.Empty;
            _cleanCts?.Dispose();
            _cleanCts = null;
        }

        // Refresh sizes and the drive bar, keeping the result line.
        await RunScanAsync(clearResult: false);
    }

    private bool CanClean() => HasSelection && !IsCleaning && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cleanCts?.Cancel();

    private bool CanStop() => IsCleaning;

    // ---------------------------------------------------------------- personal files

    private async Task LoadBigFilesAsync(CancellationToken token)
    {
        var options = LargeFileSearchOptions.BigFiles(
            _paths.PersonalFolders, _settings.Current.Cleanup.LargeFileThresholdMb);
        Replace(BigFiles, await _fileFinder.FindAsync(options, token));
    }

    private async Task LoadOldDownloadsAsync(CancellationToken token)
    {
        var folder = _paths.DownloadsFolder;
        Replace(OldDownloads, await _fileFinder.FindAsync(LargeFileSearchOptions.OldDownloads(folder), token));
    }

    private static void Replace(ObservableCollection<CleanupFileRowViewModel> target, IReadOnlyList<FoundFile> files)
    {
        target.Clear();
        foreach (var file in files)
        {
            target.Add(new CleanupFileRowViewModel(file));
        }
    }

    [RelayCommand]
    private void ShowInFolder(CleanupFileRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            // "/select," and the path as separate arguments: the runner quotes the path if needed.
            _processRunner.StartDetached("explorer.exe", ["/select,", row.File.FullPath]);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not open Explorer for {Path}.", row.File.FullPath);
            FilesMessage = "Porchlight could not open that folder.";
        }
    }

    [RelayCommand]
    private async Task MoveToRecycleBinAsync(CleanupFileRowViewModel? row)
    {
        if (row is null || IsCleaning)
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

        FilesMessage = string.Empty;
        var path = row.File.FullPath;
        var moved = await Task.Run(() => _recycler.MoveToRecycleBin(path));
        if (moved)
        {
            BigFiles.Remove(row);
            OldDownloads.Remove(row);
            RefreshDrive();
        }
        else
        {
            FilesMessage = $"Porchlight could not move \"{row.Name}\" to the Recycle Bin. It may be open in another program.";
        }
    }

    // ---------------------------------------------------------------- apps

    private async Task LoadAppsAsync(CancellationToken token)
    {
        var apps = await Task.Run(_appsReader.GetInstalledApps, token);
        var today = DateOnly.FromDateTime(DateTime.Today);
        _allApps = apps.Select(app => new CleanupAppRowViewModel(app, _uninstaller.CanStartUninstall(app), today)).ToList();
        ShowApps(all: false);
    }

    private void ShowApps(bool all)
    {
        Apps.Clear();
        foreach (var row in all ? _allApps : _allApps.Take(TopAppCount))
        {
            Apps.Add(row);
        }

        CanShowAllApps = !all && _allApps.Count > TopAppCount;
    }

    [RelayCommand]
    private void ShowAllApps() => ShowApps(all: true);

    [RelayCommand]
    private void Uninstall(CleanupAppRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_confirmation.Confirm(
                "Uninstall this app?",
                $"Open the uninstaller for \"{row.Name}\"?\n\nThe app's own uninstall window will appear and you can still cancel there."))
        {
            return;
        }

        AppsMessage = _uninstaller.StartUninstall(row.App) switch
        {
            UninstallStartResult.Started =>
                $"The uninstaller for \"{row.Name}\" is open. When it finishes, choose Scan again to refresh this list.",
            UninstallStartResult.BlockedWhileElevated =>
                "For safety, uninstalling is turned off while Porchlight runs as administrator. Use Installed apps in Windows instead.",
            _ => $"Porchlight could not start the uninstaller for \"{row.Name}\". Try Installed apps in Windows instead.",
        };
    }

    [RelayCommand]
    private void OpenInstalledApps() => _urlLauncher.Open(InstalledAppsUri);

    [RelayCommand]
    private void OpenStorageSettings() => _urlLauncher.Open(StorageSettingsUri);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCts.Cancel();
        DiskMap.Dispose();
        Duplicates.Dispose();
        _cleanCts?.Cancel();
        _scanCts?.Dispose();
        _lifetimeCts.Dispose();
    }
}
