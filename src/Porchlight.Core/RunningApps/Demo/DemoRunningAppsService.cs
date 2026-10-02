#if DEBUG
namespace Porchlight.Core.RunningApps.Demo;

/// <summary>DEBUG-only fake <see cref="IRunningAppsService"/> used when
/// <see cref="Monitoring.Demo.DemoDataMode.IsEnabled"/> is set: a made-up process list whose numbers
/// drift slowly. "Ending" an app just removes it from the fake list.</summary>
internal sealed class DemoRunningAppsService : IRunningAppsService
{
    private const long Mb = 1024 * 1024;
    private const long TotalMemoryBytes = 16L * 1024 * Mb;

    private sealed record Entry(string Key, string Name, RunningAppSection Section, bool CanEnd, int Count, double BaseCpu, long BaseMemory);

    private readonly object _lock = new();
    private readonly List<Entry> _entries =
    [
        new("demo-browser", "Demo Browser", RunningAppSection.Apps, true, 12, 6.0, 1800 * Mb),
        new("demo-chat", "Demo Chat App", RunningAppSection.Apps, true, 4, 2.5, 640 * Mb),
        new("demo-media", "Demo Media Player", RunningAppSection.Apps, true, 1, 1.2, 210 * Mb),
        new("demo-editor", "Demo Text Editor", RunningAppSection.Apps, true, 2, 0.6, 180 * Mb),
        new("demo-sync", "Demo Cloud Sync", RunningAppSection.Background, true, 1, 0.8, 260 * Mb),
        new("demo-updater", "Demo Updater", RunningAppSection.Background, true, 1, 0.1, 24 * Mb),
        new("demo-service", "Demo Background Service", RunningAppSection.Background, true, 2, 0.2, 45 * Mb),
        new("demo-windows-shell", "Windows Shell", RunningAppSection.Windows, false, 1, 1.4, 190 * Mb),
        new("demo-windows-services", "Windows Services", RunningAppSection.Windows, false, 9, 0.9, 320 * Mb),
    ];

    private int _tick;

    public Task<RunningAppsSnapshot> SampleAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _tick++;
            var apps = _entries
                .Select((entry, index) =>
                {
                    var wave = Math.Sin((_tick / 4.0) + index);
                    var cpu = Math.Max(0, entry.BaseCpu * (1 + (0.4 * wave)));
                    var memory = entry.BaseMemory + (long)(entry.BaseMemory * 0.03 * wave);
                    return new RunningApp(entry.Key, entry.Name, null, entry.Section, entry.CanEnd, entry.Count, cpu, memory);
                })
                .ToList();

            var memoryUsed = 9_400L * Mb + apps.Sum(app => app.MemoryBytes) / 20;
            var snapshot = new RunningAppsSnapshot(
                apps,
                Math.Min(100, apps.Sum(app => app.CpuPercent)),
                new MemoryStatus(TotalMemoryBytes, TotalMemoryBytes - memoryUsed));
            return Task.FromResult(snapshot);
        }
    }

    public Task<EndTaskResult> EndAsync(string groupKey, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Key == groupKey);
            if (entry is null)
            {
                return Task.FromResult(EndTaskResult.NotFound);
            }

            if (!entry.CanEnd)
            {
                return Task.FromResult(EndTaskResult.Refused);
            }

            _entries.Remove(entry);
            return Task.FromResult(EndTaskResult.Ended);
        }
    }
}
#endif
