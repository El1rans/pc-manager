using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Monitoring;

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
    private bool _initialized;

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
        // freeze the window before it is even shown. Deferred to the first Sample() call instead,
        // which always runs on the dashboard's background sampling loop thread (see
        // DashboardViewModel.RunAsync). Sample() is only ever called sequentially from that one
        // thread, so EnsureInitialized() does not need to guard against concurrent callers, but the
        // lock is kept cheap insurance against that assumption changing later.
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

            _networkStopwatch.Start();

            _initialized = true;
        }
    }

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
