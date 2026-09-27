using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PCManager.App.Shell;
using PCManager.Core.Monitoring;

namespace PCManager.App.Features.Dashboard;

/// <summary>
/// Live overview of the PC: metric tiles (CPU/Memory/GPU/Disk/Download/Upload), drives, top
/// processes and static system info. Runs its own 1-second sampling loop off the UI thread for the
/// whole app lifetime (history is kept while the user is on another page), pausing while the main
/// window is minimized. See docs/specs/03-dashboard.md.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModelBase, IDisposable
{
    private const int SampleIntervalSeconds = 1;
    private const int ProcessSampleEveryNTicks = 2;
    private const int DriveSampleEveryNTicks = 15;
    private const int RestartCheckEveryNTicks = 60;
    private const int TopProcessCount = 8;

    private readonly IPerformanceSampler _performanceSampler;
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IProcessMonitor _processMonitor;
    private readonly IDriveMonitor _driveMonitor;
    private readonly IRestartDetector _restartDetector;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly CancellationTokenSource _cts = new();

    private SystemInfo? _systemInfo;

    public DashboardViewModel(
        IPerformanceSampler performanceSampler,
        ISystemInfoProvider systemInfoProvider,
        IProcessMonitor processMonitor,
        IDriveMonitor driveMonitor,
        IRestartDetector restartDetector,
        ILogger<DashboardViewModel> logger)
    {
        _performanceSampler = performanceSampler;
        _systemInfoProvider = systemInfoProvider;
        _processMonitor = processMonitor;
        _driveMonitor = driveMonitor;
        _restartDetector = restartDetector;
        _logger = logger;

        MetricTiles = [CpuTile, MemoryTile, GpuTile, DiskTile, DownloadTile, UploadTile];

        // Kept running for the app's whole lifetime - started here rather than in
        // OnNavigatedToAsync, and cancelled/disposed with this singleton at shutdown (this view
        // model is a DI singleton; see DashboardFeature/AddPage). Wrapped in Task.Run so even the
        // synchronous prologue (before the first await) never touches the UI thread.
        _ = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    public override string Title => "Dashboard";

    public override string Glyph => "";

    public override int Order => 0;

    [ObservableProperty]
    private string _subtitleText = "Loading...";

    [ObservableProperty]
    private bool _isRestartPending;

    public MetricTileViewModel CpuTile { get; } = new("CPU", 100, v => $"{v:0}%");

    public MetricTileViewModel MemoryTile { get; } = new("Memory", 100, v => $"{v:0}%");

    public MetricTileViewModel GpuTile { get; } = new("GPU", 100, v => $"{v:0}%");

    public MetricTileViewModel DiskTile { get; } = new("Disk", 100, v => $"{v:0}%");

    public MetricTileViewModel DownloadTile { get; } = new("Download", double.NaN, ByteFormatter.FormatBitRate);

    public MetricTileViewModel UploadTile { get; } = new("Upload", double.NaN, ByteFormatter.FormatBitRate);

    /// <summary>Bound to a 3-column grid in <c>DashboardView</c>.</summary>
    public IReadOnlyList<MetricTileViewModel> MetricTiles { get; }

    public ObservableCollection<DriveRowViewModel> Drives { get; } = [];

    public ObservableCollection<ProcessRowViewModel> TopProcesses { get; } = [];

    public ObservableCollection<SystemInfoRowViewModel> SystemInfoRows { get; } = [];

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await LoadSystemInfoAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(SampleIntervalSeconds));
        var tick = 0L;
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                tick++;

                if (await IsMinimizedAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Skip the (comparatively expensive) sampling work entirely while minimized,
                    // not just the UI update - that is the point of pausing.
                    continue;
                }

                await SamplePerformanceAsync().ConfigureAwait(false);

                if (tick % ProcessSampleEveryNTicks == 0)
                {
                    await SampleProcessesAsync().ConfigureAwait(false);
                }

                if (tick % DriveSampleEveryNTicks == 0)
                {
                    await SampleDrivesAsync().ConfigureAwait(false);
                }

                if (tick % RestartCheckEveryNTicks == 0)
                {
                    await SampleRestartPendingAsync().ConfigureAwait(false);
                }

                await UpdateSubtitleAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown (Dispose cancels _cts).
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard sampling loop stopped unexpectedly.");
        }
    }

    private async Task LoadSystemInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var info = await _systemInfoProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            _systemInfo = info;

            await RunOnUiThreadAsync(() =>
            {
                SystemInfoRows.Clear();
                foreach (var row in BuildSystemInfoRows(info))
                {
                    SystemInfoRows.Add(row);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    private static IEnumerable<SystemInfoRowViewModel> BuildSystemInfoRows(SystemInfo info)
    {
        yield return new SystemInfoRowViewModel("Computer name", info.ComputerName);
        yield return new SystemInfoRowViewModel("Operating system", $"{info.OsCaption} (build {info.OsBuild})");
        yield return new SystemInfoRowViewModel("Manufacturer / model", $"{info.Manufacturer} {info.Model}");
        yield return new SystemInfoRowViewModel("Processor", info.CpuName);
        yield return new SystemInfoRowViewModel(
            "Cores / logical processors",
            $"{info.PhysicalCores} / {info.LogicalProcessors}");
        yield return new SystemInfoRowViewModel(
            "Graphics",
            info.GpuNames.Count == 0 ? "Unknown" : string.Join(", ", info.GpuNames));
        yield return new SystemInfoRowViewModel("Memory", ByteFormatter.FormatBytes(info.TotalRamBytes));
    }

    private async Task SamplePerformanceAsync()
    {
        var snapshot = _performanceSampler.Sample();

        await RunOnUiThreadAsync(() =>
        {
            CpuTile.Update(snapshot.CpuPercent, FormatPercent(snapshot.CpuPercent), string.Empty);

            var memoryPercent = snapshot is { MemoryUsedBytes: { } used, MemoryTotalBytes: { } total } && total > 0
                ? 100.0 * used / total
                : (double?)null;
            var memoryDetail = memoryPercent is null
                ? string.Empty
                : $"{ByteFormatter.FormatBytes(snapshot.MemoryUsedBytes!.Value)} of {ByteFormatter.FormatBytes(snapshot.MemoryTotalBytes!.Value)}";
            MemoryTile.Update(memoryPercent, FormatPercent(memoryPercent), memoryDetail);

            GpuTile.Update(snapshot.GpuPercent, FormatPercent(snapshot.GpuPercent), string.Empty);

            var diskDetail = snapshot is { DiskReadBytesPerSecond: { } read, DiskWriteBytesPerSecond: { } write }
                ? $"Read {ByteFormatter.FormatByteRate(read)} - Write {ByteFormatter.FormatByteRate(write)}"
                : string.Empty;
            DiskTile.Update(snapshot.DiskActivePercent, FormatPercent(snapshot.DiskActivePercent), diskDetail);

            DownloadTile.Update(
                snapshot.NetworkDownloadBytesPerSecond,
                FormatRate(snapshot.NetworkDownloadBytesPerSecond),
                string.Empty);
            UploadTile.Update(
                snapshot.NetworkUploadBytesPerSecond,
                FormatRate(snapshot.NetworkUploadBytesPerSecond),
                string.Empty);
        }).ConfigureAwait(false);
    }

    private async Task SampleProcessesAsync()
    {
        var top = _processMonitor.SampleTop(TopProcessCount);

        await RunOnUiThreadAsync(() =>
        {
            TopProcesses.Clear();
            foreach (var group in top)
            {
                TopProcesses.Add(new ProcessRowViewModel(group));
            }
        }).ConfigureAwait(false);
    }

    private async Task SampleDrivesAsync()
    {
        var drives = _driveMonitor.GetDrives();

        await RunOnUiThreadAsync(() =>
        {
            Drives.Clear();
            foreach (var drive in drives)
            {
                Drives.Add(new DriveRowViewModel(drive));
            }
        }).ConfigureAwait(false);
    }

    private async Task SampleRestartPendingAsync()
    {
        var pending = _restartDetector.IsRestartPending();
        await RunOnUiThreadAsync(() => IsRestartPending = pending).ConfigureAwait(false);
    }

    private async Task UpdateSubtitleAsync()
    {
        if (_systemInfo is not { } info)
        {
            return;
        }

        var subtitle = info.LastBootTimeUtc is { } bootTimeUtc
            ? $"{info.ComputerName} - {info.OsCaption} - up {ByteFormatter.FormatDuration(DateTime.UtcNow - bootTimeUtc)}"
            : $"{info.ComputerName} - {info.OsCaption}";

        await RunOnUiThreadAsync(() => SubtitleText = subtitle).ConfigureAwait(false);
    }

    private static string FormatPercent(double? value) => value is { } v ? $"{v:0}%" : "n/a";

    private static string FormatRate(double? bytesPerSecond) =>
        bytesPerSecond is { } v ? ByteFormatter.FormatBitRate(v) : "n/a";

    /// <summary>Whether the main window is minimized, read via the dispatcher since
    /// <see cref="Window"/> is thread-affine and this loop runs on a background thread.</summary>
    private static async Task<bool> IsMinimizedAsync(CancellationToken cancellationToken)
    {
        var app = Application.Current;
        if (app is null)
        {
            return false;
        }

        return await app.Dispatcher.InvokeAsync(
            () => app.MainWindow?.WindowState == WindowState.Minimized,
            DispatcherPriority.Background,
            cancellationToken).Task.ConfigureAwait(false);
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        var app = Application.Current;
        if (app is null)
        {
            // No WPF application (e.g. a unit test host); run inline rather than losing the update.
            action();
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task;
    }
}
