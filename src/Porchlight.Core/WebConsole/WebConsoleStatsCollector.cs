using Microsoft.Extensions.Logging;
using Porchlight.Core.Hardware;
using Porchlight.Core.Monitoring;
using Porchlight.Core.Settings;

namespace Porchlight.Core.WebConsole;

/// <summary>
/// Samples the PC on demand for the web console - only while a browser is actually asking, so an
/// enabled-but-unwatched console costs nothing. Owns its own <see cref="IPerformanceSampler"/> and
/// <see cref="IProcessMonitor"/> (both keep "previous sample" state to compute rates), separate from
/// the dashboard's, so the two never skew each other's numbers and the console keeps working while
/// the dashboard is paused (main window minimized). Hardware tiles come from the already-running
/// <see cref="IHardwareService"/>'s latest snapshot. Everything here only reads.
/// </summary>
public sealed class WebConsoleStatsCollector : IWebConsoleStatsSource, IDisposable
{
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IPerformanceSampler _performanceSampler;
    private readonly IProcessMonitor _processMonitor;
    private readonly IDriveMonitor _driveMonitor;
    private readonly IRestartDetector _restartDetector;
    private readonly IHardwareService _hardwareService;
    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _rePrimeDelay;
    private readonly ILogger<WebConsoleStatsCollector> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private WebConsoleStats? _cached;
    private DateTimeOffset _cachedAt;
    private DateTimeOffset _lastPerformanceSampleAt;
    private SystemInfo? _systemInfo;
    private IReadOnlyList<DriveSnapshot> _drives = [];
    private DateTimeOffset _drivesReadAt;
    private bool _isRestartPending;
    private DateTimeOffset _restartCheckedAt;
    private bool _disposed;

    /// <param name="performanceSampler">Owned: disposed with this collector. Must not be shared
    /// with another consumer (see the class remarks).</param>
    /// <param name="processMonitor">Must not be shared with another consumer.</param>
    public WebConsoleStatsCollector(
        ISystemInfoProvider systemInfoProvider,
        IPerformanceSampler performanceSampler,
        IProcessMonitor processMonitor,
        IDriveMonitor driveMonitor,
        IRestartDetector restartDetector,
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        ILogger<WebConsoleStatsCollector> logger)
        : this(
            systemInfoProvider, performanceSampler, processMonitor, driveMonitor, restartDetector,
            hardwareService, settingsStore, logger, TimeProvider.System, WebConsoleOptions.RePrimeDelay)
    {
    }

    /// <summary>Test seam: a fake clock and a zero re-prime delay, so tests never wait on real time.</summary>
    internal WebConsoleStatsCollector(
        ISystemInfoProvider systemInfoProvider,
        IPerformanceSampler performanceSampler,
        IProcessMonitor processMonitor,
        IDriveMonitor driveMonitor,
        IRestartDetector restartDetector,
        IHardwareService hardwareService,
        ISettingsStore settingsStore,
        ILogger<WebConsoleStatsCollector> logger,
        TimeProvider timeProvider,
        TimeSpan rePrimeDelay)
    {
        _systemInfoProvider = systemInfoProvider;
        _performanceSampler = performanceSampler;
        _processMonitor = processMonitor;
        _driveMonitor = driveMonitor;
        _restartDetector = restartDetector;
        _hardwareService = hardwareService;
        _settingsStore = settingsStore;
        _logger = logger;
        _timeProvider = timeProvider;
        _rePrimeDelay = rePrimeDelay;
    }

    public async Task<WebConsoleStats> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (_cached is not null && now - _cachedAt < WebConsoleOptions.MinSampleInterval)
            {
                return _cached;
            }

            _systemInfo ??= await TryLoadSystemInfoAsync(cancellationToken).ConfigureAwait(false);

            if (now - _lastPerformanceSampleAt > WebConsoleOptions.StaleSampleThreshold)
            {
                // Nobody has looked for a while (or ever): the rate-based sources would report an
                // average over that whole gap. Re-prime them, then sample for real a moment later.
                TryRun("re-prime", () =>
                {
                    _performanceSampler.Sample();
                    _processMonitor.SampleTop(WebConsoleOptions.TopProcessCount);
                });
                await Task.Delay(_rePrimeDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            var performance = TryRead("performance", _performanceSampler.Sample);
            var processes = TryRead("processes", () => _processMonitor.SampleTop(WebConsoleOptions.TopProcessCount)) ?? [];
            now = _timeProvider.GetUtcNow();
            _lastPerformanceSampleAt = now;

            if (now - _drivesReadAt >= WebConsoleOptions.DriveRefreshInterval)
            {
                _drives = TryRead("drives", _driveMonitor.GetDrives) ?? _drives;
                _drivesReadAt = now;
            }

            if (now - _restartCheckedAt >= WebConsoleOptions.RestartCheckInterval)
            {
                _isRestartPending = TryReadFlag("restart-check", () => (bool?)_restartDetector.IsRestartPending()) ?? _isRestartPending;
                _restartCheckedAt = now;
            }

            _cached = new WebConsoleStats(
                now,
                _systemInfo,
                performance,
                _drives,
                processes,
                _isRestartPending,
                BuildHardwareTiles());
            _cachedAt = now;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _performanceSampler.Dispose();
        _gate.Dispose();
    }

    private IReadOnlyList<HardwareSummaryTile> BuildHardwareTiles()
    {
        var hardware = _settingsStore.Current.Hardware;
        return TryRead("hardware", () => HardwareSummarySelector.Build(
            _hardwareService.Latest,
            hardware.FailsafeTemperatureC,
            hardware.FanDisplayNames)) ?? [];
    }

    private async Task<SystemInfo?> TryLoadSystemInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _systemInfoProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Left null: the next request tries again, and the page shows the rest meanwhile.
            _logger.LogWarning(ex, "Web console could not read system info; will retry on the next request.");
            return null;
        }
    }

    /// <summary>Runs one sampling step, isolating its failure so one broken source (e.g. WMI
    /// disabled) never takes the whole response down with it.</summary>
    private T? TryRead<T>(string step, Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Web console sampling step {Step} failed; serving without it.", step);
            }
            return null;
        }
    }

    private bool? TryReadFlag(string step, Func<bool?> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Web console sampling step {Step} failed; serving without it.", step);
            }
            return null;
        }
    }

    private void TryRun(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "Web console sampling step {Step} failed; serving without it.", step);
            }
        }
    }
}
