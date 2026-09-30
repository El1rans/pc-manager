using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Monitoring;

/// <inheritdoc cref="IPerformanceSampler"/>
public sealed partial class PerformanceSampler : IPerformanceSampler
{
    private const string GpuEngineCategory = "GPU Engine";
    private const string GpuUtilizationCounter = "Utilization Percentage";

    private readonly ILogger<PerformanceSampler> _logger;
    private readonly Stopwatch _networkStopwatch = new();
    private readonly object _initLock = new();

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskIdleCounter;
    private PerformanceCounter? _diskReadCounter;
    private PerformanceCounter? _diskWriteCounter;
    private bool _gpuCategoryAvailable;

    /// <summary>Volatile: set by <see cref="WarmUp"/> on one thread and read by
    /// <see cref="SampleWithoutWaiting"/> on another.</summary>
    private volatile bool _initialized;

    /// <summary>Previous <c>GetSystemTimes</c> reading, for the CPU value reported while the
    /// counters are still warming up (see <see cref="SampleCpuFromSystemTimes"/>).</summary>
    private (long Idle, long Kernel, long User)? _previousSystemTimes;

    private Dictionary<string, CounterSample> _previousGpuSamples = [];
    private Dictionary<string, NetworkAdapterSample> _previousNetworkSamples = new(StringComparer.Ordinal);
    private long _cumulativeDownloadBytes;
    private long _cumulativeUploadBytes;

    public PerformanceSampler(ILogger<PerformanceSampler> logger)
    {
        _logger = logger;

        // Counter creation/priming (and the first GPU ReadCategory call) is slow - measured at
        // ~1 second - so it must not run in the constructor: DI resolves this singleton on the UI
        // thread (as a MainViewModel/MainWindow dependency chain), and doing this work there would
        // freeze the window before it is even shown. Deferred to WarmUp() (the dashboard starts it
        // on a background thread as soon as it is created) or the first Sample() call, whichever
        // comes first; the lock makes the two safe to race.
        //
        // Meanwhile CPU comes from GetSystemTimes, which is instant - baseline it now so the very
        // first sample already has an interval to measure over.
        _previousSystemTimes = ReadSystemTimes();
    }

    /// <summary>
    /// Nearly all of the ~1 second setup (measured 940-990 ms warm, 5 s+ right after boot) is
    /// Windows loading the performance-counter library on the first counter call in the process;
    /// every counter after that takes milliseconds. Call this as early as possible at app startup,
    /// on a background thread, so that load overlaps building the window instead of starting only
    /// once the dashboard exists. Best-effort and never throws.
    /// </summary>
    public static void PrewarmCounterLibrary()
    {
        try
        {
            PerformanceCounterCategory.Exists("Processor Information");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            // Nothing to prewarm; EnsureInitialized() logs and degrades on its own later.
        }
    }

    public bool IsWarmedUp => _initialized;

    public void WarmUp() => EnsureInitialized();

    public PerformanceSnapshot SampleWithoutWaiting()
    {
        if (_initialized)
        {
            return Sample();
        }

        // Memory, network and (via GetSystemTimes) CPU need no counters, so they can show while
        // WarmUp() is still running.
        var cpuPercent = SampleCpuFromSystemTimes();
        var (memoryUsed, memoryTotal) = SampleMemory();
        var (download, upload) = SampleNetwork();
        return new PerformanceSnapshot(
            cpuPercent,
            memoryUsed,
            memoryTotal,
            null,
            null,
            null,
            null,
            download,
            upload,
            _cumulativeDownloadBytes,
            _cumulativeUploadBytes);
    }

    public PerformanceSnapshot Sample()
    {
        EnsureInitialized();

        var cpuPercent = SafeRead(_cpuCounter, v => Math.Clamp(v, 0, 100));
        var (memoryUsed, memoryTotal) = SampleMemory();
        var gpuPercent = _gpuCategoryAvailable ? SampleGpu() : null;
        var diskIdle = SafeRead(_diskIdleCounter, v => v);
        var diskActive = diskIdle is null ? (double?)null : Math.Clamp(100 - diskIdle.Value, 0, 100);
        var diskRead = SafeRead(_diskReadCounter, v => v);
        var diskWrite = SafeRead(_diskWriteCounter, v => v);
        var (download, upload) = SampleNetwork();

        return new PerformanceSnapshot(
            cpuPercent,
            memoryUsed,
            memoryTotal,
            gpuPercent,
            diskActive,
            diskRead,
            diskWrite,
            download,
            upload,
            _cumulativeDownloadBytes,
            _cumulativeUploadBytes);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _diskIdleCounter?.Dispose();
        _diskReadCounter?.Dispose();
        _diskWriteCounter?.Dispose();
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            // "Processor Information / % Processor Utility" matches Task Manager on modern
            // Windows; "Processor / % Processor Time" is the classic fallback for older/locked-down
            // machines.
            _cpuCounter = CreateCounter("Processor Information", "% Processor Utility", "_Total")
                ?? CreateCounter("Processor", "% Processor Time", "_Total");

            _diskIdleCounter = CreateCounter("PhysicalDisk", "% Idle Time", "_Total");
            _diskReadCounter = CreateCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            _diskWriteCounter = CreateCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

            _gpuCategoryAvailable = CategoryExists(GpuEngineCategory);

            // Prime rate counters: the first NextValue() after creation is always 0.
            _cpuCounter?.NextValue();
            _diskIdleCounter?.NextValue();
            _diskReadCounter?.NextValue();
            _diskWriteCounter?.NextValue();
            if (_gpuCategoryAvailable)
            {
                _previousGpuSamples = ReadGpuSamples() ?? [];
            }

            _initialized = true;
        }
    }

    /// <summary>CPU busy % since the previous call, from <c>GetSystemTimes</c> (kernel time includes
    /// idle time, so busy = 1 - idle / (kernel + user)). Only used until the counters are ready: it
    /// is instant, but reads a little differently from Task Manager's "% Processor Utility" on a
    /// CPU that boosts. Null when there is no previous reading or no time has passed.</summary>
    private double? SampleCpuFromSystemTimes()
    {
        var current = ReadSystemTimes();
        var previous = _previousSystemTimes;
        _previousSystemTimes = current;
        if (current is not { } now || previous is not { } before)
        {
            return null;
        }

        var idle = now.Idle - before.Idle;
        var total = (now.Kernel - before.Kernel) + (now.User - before.User);
        return total <= 0 ? null : Math.Clamp(100.0 * (total - idle) / total, 0, 100);
    }

    private static (long Idle, long Kernel, long User)? ReadSystemTimes() =>
        GetSystemTimes(out var idle, out var kernel, out var user) ? (idle, kernel, user) : null;

    private static (long? Used, long? Total) SampleMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return (null, null);
        }

        var total = (long)status.TotalPhysicalBytes;
        var used = total - (long)status.AvailablePhysicalBytes;
        return (used, total);
    }

    private double? SampleGpu()
    {
        var currentSamples = ReadGpuSamples();
        if (currentSamples is null)
        {
            // The read itself failed (category vanished, access denied, ...) - genuinely unknown
            // this tick, not "0% busy".
            return null;
        }

        var deltas = new List<(string InstanceName, double Value)>();
        foreach (var (instanceName, current) in currentSamples)
        {
            if (_previousGpuSamples.TryGetValue(instanceName, out var previous))
            {
                deltas.Add((instanceName, CounterSample.Calculate(previous, current)));
            }
        }

        _previousGpuSamples = currentSamples;
        return deltas.Count == 0 ? 0 : GpuEngineAggregator.Aggregate(deltas);
    }

    /// <summary>Reads every GPU Engine instance's Utilization Percentage in one call
    /// (<see cref="PerformanceCounterCategory.ReadCategory"/>) rather than creating one
    /// <see cref="PerformanceCounter"/> per process/engine instance, which is far cheaper - GPU
    /// Engine instances churn constantly as processes start/stop using the GPU. Null means the read
    /// itself failed (source unavailable this tick); an empty (non-null) dictionary means the read
    /// succeeded but no process is currently using the GPU.</summary>
    private Dictionary<string, CounterSample>? ReadGpuSamples()
    {
        try
        {
            var category = new PerformanceCounterCategory(GpuEngineCategory);
            var data = category.ReadCategory();
            var samples = new Dictionary<string, CounterSample>(StringComparer.OrdinalIgnoreCase);
            if (!data.Contains(GpuUtilizationCounter))
            {
                return samples;
            }

            foreach (DictionaryEntry entry in data[GpuUtilizationCounter])
            {
                if (entry.Key is string instanceName && entry.Value is InstanceData instanceData)
                {
                    samples[instanceName] = instanceData.Sample;
                }
            }

            return samples;
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            LogGpuCategoryReadFailed(ex, GpuEngineCategory);
            return null;
        }
    }

    private (double? Download, double? Upload) SampleNetwork()
    {
        try
        {
            var current = ReadNetworkAdapterSamples();

            // Zero on the very first call (the stopwatch has not started yet), which reports "no
            // rate yet" below rather than a meaningless rate over a near-zero interval.
            var elapsed = _networkStopwatch.Elapsed;
            _networkStopwatch.Restart();

            var (downloadBytes, uploadBytes) = NetworkThroughputCalculator.CalculateDelta(_previousNetworkSamples, current);
            _previousNetworkSamples = ToDictionaryByAdapterId(current);
            _cumulativeDownloadBytes += downloadBytes;
            _cumulativeUploadBytes += uploadBytes;

            if (elapsed <= TimeSpan.Zero)
            {
                return (null, null);
            }

            return (downloadBytes / elapsed.TotalSeconds, uploadBytes / elapsed.TotalSeconds);
        }
        catch (NetworkInformationException ex)
        {
            LogNetworkSampleFailed(ex);
            return (null, null);
        }
    }

    private static List<NetworkAdapterSample> ReadNetworkAdapterSamples()
    {
        var samples = new List<NetworkAdapterSample>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var stats = nic.GetIPStatistics();
            samples.Add(new NetworkAdapterSample(nic.Id, stats.BytesReceived, stats.BytesSent));
        }

        return samples;
    }

    private static Dictionary<string, NetworkAdapterSample> ToDictionaryByAdapterId(List<NetworkAdapterSample> samples)
    {
        var dictionary = new Dictionary<string, NetworkAdapterSample>(samples.Count, StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            dictionary[sample.AdapterId] = sample;
        }

        return dictionary;
    }

    private PerformanceCounter? CreateCounter(string category, string counter, string instance)
    {
        try
        {
            if (!CategoryExists(category))
            {
                return null;
            }

            return new PerformanceCounter(category, counter, instance, readOnly: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            LogCounterUnavailable(ex, category, counter);
            return null;
        }
    }

    private bool CategoryExists(string category)
    {
        try
        {
            return PerformanceCounterCategory.Exists(category);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            LogCategoryCheckFailed(ex, category);
            return false;
        }
    }

    private double? SafeRead(PerformanceCounter? counter, Func<double, double> transform)
    {
        if (counter is null)
        {
            return null;
        }

        try
        {
            return transform(counter.NextValue());
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or Win32Exception)
        {
            LogCounterReadFailed(ex, counter.CounterName);
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalBytes;
        public ulong AvailablePhysicalBytes;
        public ulong TotalPageFileBytes;
        public ulong AvailablePageFileBytes;
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    // DllImport rather than the LibraryImport source generator: the generator's marshalling for a
    // ref struct with a bool return needs AllowUnsafeBlocks, which is not otherwise needed here.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
#pragma warning restore SYSLIB1054

    // Source-generated (guarded by IsEnabled internally) so the message is never formatted when
    // Debug logging is disabled - see CA1873. All failures here are expected degraded-hardware
    // paths (missing counter category, access denied), never surfaced to the user.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to read {Category} category.")]
    private partial void LogGpuCategoryReadFailed(Exception ex, string category);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to sample network interfaces.")]
    private partial void LogNetworkSampleFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Performance counter {Category}/{Counter} unavailable.")]
    private partial void LogCounterUnavailable(Exception ex, string category, string counter);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to check for performance counter category {Category}.")]
    private partial void LogCategoryCheckFailed(Exception ex, string category);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to read performance counter {CounterName}.")]
    private partial void LogCounterReadFailed(Exception ex, string counterName);
}
