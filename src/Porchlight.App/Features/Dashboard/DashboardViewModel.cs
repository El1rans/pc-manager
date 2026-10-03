using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Controls;
using Porchlight.App.Features.Cleanup;
using Porchlight.App.Shell;
using Porchlight.Core.Monitoring;

namespace Porchlight.App.Features.Dashboard;

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

    /// <summary>How long <see cref="Dispose"/> waits for the sampling loop to notice cancellation
    /// and exit before giving up (the loop is a background task; this keeps shutdown bounded rather
    /// than hanging on a slow WMI/counter call).</summary>
    private static readonly TimeSpan LoopShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Minimum gap between repeated "this tick step keeps failing" warnings for the same
    /// step, so a persistently broken source (e.g. WMI disabled) logs once in a while rather than
    /// once a second forever.</summary>
    private static readonly TimeSpan TickFailureLogInterval = TimeSpan.FromSeconds(30);

    private readonly IPerformanceSampler _performanceSampler;
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IProcessMonitor _processMonitor;
    private readonly IDriveMonitor _driveMonitor;
    private readonly IRestartDetector _restartDetector;
    private readonly IPageNavigator _navigator;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private readonly Dictionary<string, DateTime> _lastTickFailureLoggedAtUtc = [];

    private Task? _loopTask;
    private SystemInfo? _systemInfo;

    public DashboardViewModel(
        IPerformanceSampler performanceSampler,
        ISystemInfoProvider systemInfoProvider,
        IProcessMonitor processMonitor,
        IDriveMonitor driveMonitor,
        IRestartDetector restartDetector,
        IPageNavigator navigator,
        ILogger<DashboardViewModel> logger)
    {
        _performanceSampler = performanceSampler;
        _systemInfoProvider = systemInfoProvider;
        _processMonitor = processMonitor;
        _driveMonitor = driveMonitor;
        _restartDetector = restartDetector;
        _navigator = navigator;
        _logger = logger;

        MetricTiles = [CpuTile, MemoryTile, GpuTile, DiskTile, DownloadTile, UploadTile];

        // Independent of the sampling loop below: a slow/hung WMI call here must never delay the
        // loop starting (see LoadSystemInfoWithLoggingAsync). Both are kept running for the app's
        // whole lifetime - started here rather than in OnNavigatedToAsync, and cancelled/awaited
        // (bounded) with this singleton at shutdown (this view model is a DI singleton; see
        // DashboardFeature/AddPage). Wrapped in Task.Run so even each method's synchronous prologue
        // (before its first await) never touches the UI thread. The performance counters' ~1 second
        // setup also runs on its own, so memory, network and processes show on the very first tick
        // instead of waiting for it; CPU/GPU/disk join as soon as it finishes.
        _ = Task.Run(() => LoadSystemInfoWithLoggingAsync(_cts.Token), _cts.Token);
        _ = Task.Run(WarmUpPerformanceSamplerWithLogging, _cts.Token);
        _loopTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    public override string Title => "Dashboard";

    public override string Glyph => "";

    public override int Order => 0;

    public override PageCategory Category => PageCategory.Overview;

    [ObservableProperty]
    private string _subtitleText = "Loading...";

    [ObservableProperty]
    private bool _isRestartPending;

    public MetricTileViewModel CpuTile { get; } = new("CPU", 100, v => $"{v:0}%", Hue.Blue, "");

    public MetricTileViewModel MemoryTile { get; } = new("Memory", 100, v => $"{v:0}%", Hue.Violet, "");

    public MetricTileViewModel GpuTile { get; } = new("GPU", 100, v => $"{v:0}%", Hue.Green, "");

    public MetricTileViewModel DiskTile { get; } = new("Disk", 100, v => $"{v:0}%", Hue.Teal, "");

    public MetricTileViewModel DownloadTile { get; } = new("Download", double.NaN, ByteFormatter.FormatBitRate, Hue.Amber, "");

    public MetricTileViewModel UploadTile { get; } = new("Upload", double.NaN, ByteFormatter.FormatBitRate, Hue.Coral, "");

    /// <summary>Bound to a 3-column grid in <c>DashboardView</c>.</summary>
    public IReadOnlyList<MetricTileViewModel> MetricTiles { get; }

    public ObservableCollection<DriveRowViewModel> Drives { get; } = [];

    /// <summary>The "Free up space" button on a low-space drive row: opens that page.</summary>
    [RelayCommand]
    private void FreeUpSpace() => _navigator.NavigateTo<CleanupViewModel>();

    public ObservableCollection<ProcessRowViewModel> TopProcesses { get; } = [];

    public ObservableCollection<SystemInfoRowViewModel> SystemInfoRows { get; } = [];

    /// <summary>Idempotent: page view models are disposed twice on host shutdown (see
    /// <see cref="Shell.PageServiceCollectionExtensions.AddPage{TViewModel, TView}"/>), and a second
    /// <see cref="CancellationTokenSource.Cancel()"/> on a disposed source would throw.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();

        try
        {
            _loopTask?.Wait(LoopShutdownTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            // Expected: the loop observed cancellation and unwound.
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(SampleIntervalSeconds));
        var tick = 0L;
        var wasMinimizedLastTick = false;

        try
        {
            // do/while so the first tick runs immediately rather than a full interval after the
            // dashboard opens; `continue` below still waits for the next tick.
            do
            {
                tick++;

                if (await IsMinimizedAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Skip the (comparatively expensive) sampling work entirely while minimized,
                    // not just the UI update - that is the point of pausing.
                    wasMinimizedLastTick = true;
                    continue;
                }

                if (wasMinimizedLastTick)
                {
                    wasMinimizedLastTick = false;

                    // The window may have been minimized for an arbitrarily long time. Rate-based
                    // sources (network/GPU deltas, this tick's process CPU-time deltas) would
                    // otherwise report a rate averaged over that whole gap, which reads as a
                    // misleading spike or trough. Take one sample now purely to re-prime their
                    // internal "previous" state, discard it, and resume showing real per-second
                    // values from the next tick.
                    _performanceSampler.SampleWithoutWaiting();
                    _processMonitor.SampleTop(TopProcessCount);
                    continue;
                }

                await RunTickStepAsync("performance", SamplePerformanceAsync, cancellationToken).ConfigureAwait(false);

                // (tick - 1) so each of these also runs on the very first tick, not only once the
                // count is first reached. Processes also run on tick 2: the first sample has no
                // previous CPU times, so it can only rank by memory - the second gives real CPU%.
                if (tick == 2 || (tick - 1) % ProcessSampleEveryNTicks == 0)
                {
                    await RunTickStepAsync("processes", SampleProcessesAsync, cancellationToken).ConfigureAwait(false);
                }

                if ((tick - 1) % DriveSampleEveryNTicks == 0)
                {
                    await RunTickStepAsync("drives", SampleDrivesAsync, cancellationToken).ConfigureAwait(false);
                }

                if ((tick - 1) % RestartCheckEveryNTicks == 0)
                {
                    await RunTickStepAsync("restart-check", SampleRestartPendingAsync, cancellationToken).ConfigureAwait(false);
                }

                await RunTickStepAsync("subtitle", UpdateSubtitleAsync, cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown (Dispose cancels _cts).
        }
    }

    /// <summary>Runs one tick step, isolating its failures so one broken source never stops the
    /// whole sampling loop for the rest of the session (previously any exception here ended
    /// sampling until the app restarted). Failures are logged at Warning, rate-limited per step.</summary>
    private async Task RunTickStepAsync(string step, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogTickStepFailureRateLimited(step, ex);
        }
    }

    private void LogTickStepFailureRateLimited(string step, Exception ex)
    {
        var now = DateTime.UtcNow;
        if (_lastTickFailureLoggedAtUtc.TryGetValue(step, out var lastLoggedAt) &&
            now - lastLoggedAt < TickFailureLogInterval)
        {
            return;
        }

        _lastTickFailureLoggedAtUtc[step] = now;
        _logger.LogWarning(ex, "Dashboard tick step {Step} failed; will keep retrying.", step);
    }

    private void WarmUpPerformanceSamplerWithLogging()
    {
        try
        {
            _performanceSampler.WarmUp();
        }
        catch (Exception ex)
        {
            // Not fatal: the sampling loop's own Sample() call retries the setup.
            _logger.LogError(ex, "Failed to warm up the performance counters for the dashboard.");
        }
    }

    private async Task LoadSystemInfoWithLoggingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadSystemInfoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            // Never let a WMI failure here block or crash the sampling loop, which is started
            // independently of this task.
            _logger.LogError(ex, "Failed to load system info for the dashboard.");
        }
    }

    private async Task LoadSystemInfoAsync(CancellationToken cancellationToken)
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
        }, cancellationToken).ConfigureAwait(false);
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

    private async Task SamplePerformanceAsync(CancellationToken cancellationToken)
    {
        // Read before sampling: if the setup finishes in between, this tick merely skips the
        // counter tiles once more, rather than counting a spurious miss against them.
        var countersReady = _performanceSampler.IsWarmedUp;
        var snapshot = _performanceSampler.SampleWithoutWaiting();
        var logicalProcessors = _systemInfo is { LogicalProcessors: > 0 } info ? info.LogicalProcessors : Environment.ProcessorCount;
        var gpuName = _systemInfo is { GpuNames.Count: > 0 } sysInfo ? sysInfo.GpuNames[0] : null;

        await RunOnUiThreadAsync(() =>
        {
            // Until the counters are ready, GPU/disk are null because they are still warming up,
            // not because they failed - leave those tiles on their neutral placeholder rather than
            // counting misses toward "Not available on this PC". CPU has a quicker source meanwhile,
            // so it shows whenever it has a value.
            if (countersReady || snapshot.CpuPercent is not null)
            {
                CpuTile.Update(snapshot.CpuPercent, FormatPercent(snapshot.CpuPercent), $"{logicalProcessors} logical processors");
            }

            var memoryPercent = snapshot is { MemoryUsedBytes: { } used, MemoryTotalBytes: { } total } && total > 0
                ? 100.0 * used / total
                : (double?)null;
            var memoryDetail = memoryPercent is null
                ? string.Empty
                : $"{ByteFormatter.FormatBytes(snapshot.MemoryUsedBytes!.Value)} of {ByteFormatter.FormatBytes(snapshot.MemoryTotalBytes!.Value)}";
            MemoryTile.Update(memoryPercent, FormatPercent(memoryPercent), memoryDetail);

            if (countersReady)
            {
                GpuTile.Update(snapshot.GpuPercent, FormatPercent(snapshot.GpuPercent), gpuName ?? string.Empty);

                var diskDetail = snapshot is { DiskReadBytesPerSecond: { } read, DiskWriteBytesPerSecond: { } write }
                    ? $"R {ByteFormatter.FormatByteRate(read)} · W {ByteFormatter.FormatByteRate(write)}"
                    : string.Empty;
                DiskTile.Update(snapshot.DiskActivePercent, FormatPercent(snapshot.DiskActivePercent), diskDetail);
            }

            var downloadDetail = $"Total: {ByteFormatter.FormatBytes(snapshot.NetworkTotalDownloadedBytes)} since start";
            DownloadTile.Update(snapshot.NetworkDownloadBytesPerSecond, FormatRate(snapshot.NetworkDownloadBytesPerSecond), downloadDetail);

            var uploadDetail = $"Total: {ByteFormatter.FormatBytes(snapshot.NetworkTotalUploadedBytes)} since start";
            UploadTile.Update(snapshot.NetworkUploadBytesPerSecond, FormatRate(snapshot.NetworkUploadBytesPerSecond), uploadDetail);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task SampleProcessesAsync(CancellationToken cancellationToken)
    {
        var top = _processMonitor.SampleTop(TopProcessCount);

        await RunOnUiThreadAsync(() =>
        {
            ApplyInPlace(TopProcesses, top, (row, snapshot) => row.Apply(snapshot), snapshot => new ProcessRowViewModel(snapshot));
            ProcessRowViewModel.UpdateMemoryShares(TopProcesses);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task SampleDrivesAsync(CancellationToken cancellationToken)
    {
        var drives = _driveMonitor.GetDrives();

        await RunOnUiThreadAsync(() => ApplyInPlace(Drives, drives, (row, snapshot) => row.Apply(snapshot), snapshot => new DriveRowViewModel(snapshot)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates an observable collection of rows in place (matching by position) instead of
    /// clearing and recreating every row every tick, which would otherwise discard and reallocate
    /// up to 8-15 view model objects a second for no visible benefit.</summary>
    private static void ApplyInPlace<TRow, TSnapshot>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TSnapshot> snapshots,
        Action<TRow, TSnapshot> apply,
        Func<TSnapshot, TRow> create)
    {
        for (var i = 0; i < snapshots.Count; i++)
        {
            if (i < rows.Count)
            {
                apply(rows[i], snapshots[i]);
            }
            else
            {
                rows.Add(create(snapshots[i]));
            }
        }

        while (rows.Count > snapshots.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }

    private async Task SampleRestartPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _restartDetector.IsRestartPending();
        await RunOnUiThreadAsync(() => IsRestartPending = pending, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateSubtitleAsync(CancellationToken cancellationToken)
    {
        if (_systemInfo is not { } info)
        {
            return;
        }

        var subtitle = info.LastBootTimeUtc is { } bootTimeUtc
            ? $"{info.ComputerName} - {info.OsCaption} - up {ByteFormatter.FormatDuration(DateTime.UtcNow - bootTimeUtc)}"
            : $"{info.ComputerName} - {info.OsCaption}";

        await RunOnUiThreadAsync(() => SubtitleText = subtitle, cancellationToken).ConfigureAwait(false);
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

    private static Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
    {
        var app = Application.Current;
        if (app is null)
        {
            // No WPF application (e.g. a unit test host); run inline rather than losing the update.
            action();
            return Task.CompletedTask;
        }

        return app.Dispatcher.InvokeAsync(action, DispatcherPriority.Background, cancellationToken).Task;
    }
}
