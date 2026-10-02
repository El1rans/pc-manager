using Microsoft.Extensions.Logging;
using Porchlight.Core.Startup;

namespace Porchlight.Core.RunningApps;

/// <inheritdoc cref="IRunningAppsService"/>
public sealed partial class RunningAppsService : IRunningAppsService, IDisposable
{
    private readonly IProcessSnapshotSource _source;
    private readonly IProcessKiller _killer;
    private readonly ISystemMemoryInfo _memory;
    private readonly IFileProductInfoReader _productInfo;
    private readonly TimeProvider _time;
    private readonly ILogger<RunningAppsService> _logger;
    private readonly string _windowsDirectory;
    private readonly int _currentProcessId;
    private readonly int _processorCount;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, ProcessSample> _previous = [];
    private long _previousTimestamp;
    private IReadOnlyList<ProcessGroup> _latest = [];

    public RunningAppsService(
        IProcessSnapshotSource source,
        IProcessKiller killer,
        ISystemMemoryInfo memory,
        IFileProductInfoReader productInfo,
        TimeProvider time,
        ILogger<RunningAppsService> logger)
        : this(source, killer, memory, productInfo, time, logger, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.ProcessId, Environment.ProcessorCount)
    {
    }

    /// <summary>Test seam: the machine facts are passed in instead of read from the environment.</summary>
    internal RunningAppsService(
        IProcessSnapshotSource source,
        IProcessKiller killer,
        ISystemMemoryInfo memory,
        IFileProductInfoReader productInfo,
        TimeProvider time,
        ILogger<RunningAppsService> logger,
        string windowsDirectory,
        int currentProcessId,
        int processorCount)
    {
        _source = source;
        _killer = killer;
        _memory = memory;
        _productInfo = productInfo;
        _time = time;
        _logger = logger;
        _windowsDirectory = windowsDirectory;
        _currentProcessId = currentProcessId;
        _processorCount = processorCount;
    }

    public void Dispose() => _gate.Dispose();

    public async Task<RunningAppsSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(SampleCore, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<EndTaskResult> EndAsync(string groupKey, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => EndCore(groupKey), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private RunningAppsSnapshot SampleCore()
    {
        var samples = _source.Capture();
        var now = _time.GetTimestamp();
        var elapsed = _previous.Count == 0 ? TimeSpan.Zero : _time.GetElapsedTime(_previousTimestamp, now);

        var cpu = CpuUsageCalculator.Calculate(_previous, samples, elapsed, _processorCount);
        var groups = ProcessGrouper.Group(samples, cpu, _windowsDirectory, _currentProcessId);

        _previous = samples.ToDictionary(sample => sample.Pid);
        _previousTimestamp = now;
        _latest = groups;

        var apps = groups
            .Select(group => new RunningApp(
                group.Key,
                FriendlyName(group),
                group.ExecutablePath,
                group.Section,
                group.CanEnd,
                group.Members.Count,
                group.CpuPercent,
                group.MemoryBytes))
            .ToList();

        var totalCpu = Math.Clamp(cpu.Values.Sum(), 0, 100);
        return new RunningAppsSnapshot(apps, totalCpu, _memory.Read());
    }

    private EndTaskResult EndCore(string groupKey)
    {
        var group = _latest.FirstOrDefault(g => string.Equals(g.Key, groupKey, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            return EndTaskResult.NotFound;
        }

        if (!group.CanEnd || group.Members.Any(m => ProcessGrouper.IsProtected(m, _windowsDirectory, _currentProcessId)))
        {
            return EndTaskResult.Refused;
        }

        var needsAdmin = false;
        var failed = false;
        foreach (var member in group.Members)
        {
            switch (_killer.Kill(member.Pid, member.StartTime))
            {
                case ProcessKillOutcome.AccessDenied:
                    needsAdmin = true;
                    break;
                case ProcessKillOutcome.Failed:
                    failed = true;
                    break;
                default:
                    // Killed, already exited, or the pid was reused: nothing left of ours to end.
                    break;
            }
        }

        LogEnded(group.Key, needsAdmin, failed);
        return needsAdmin ? EndTaskResult.NeedsAdmin : failed ? EndTaskResult.Failed : EndTaskResult.Ended;
    }

    private string FriendlyName(ProcessGroup group)
    {
        if (group.ExecutablePath is null)
        {
            return group.Name;
        }

        if (_nameCache.TryGetValue(group.ExecutablePath, out var cached))
        {
            return cached;
        }

        var info = _productInfo.Read(group.ExecutablePath);
        var name = FirstNonBlank(info?.Description, info?.Product)
            ?? Path.GetFileNameWithoutExtension(group.ExecutablePath);
        _nameCache[group.ExecutablePath] = name;
        return name;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    [LoggerMessage(Level = LogLevel.Information, Message = "Ended task {Key} (needs admin: {NeedsAdmin}, failed: {Failed}).")]
    private partial void LogEnded(string key, bool needsAdmin, bool failed);
}
