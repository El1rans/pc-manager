namespace Porchlight.Core.RunningApps;

/// <summary>Pure logic: groups processes by executable and decides section and whether they may be ended.</summary>
public static class ProcessGrouper
{
    private const int ServicesSessionId = 0;
    private const double MaxCpuPercent = 100;

    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass",
        "svchost", "dwm", "fontdrvhost", "Registry", "MemCompression", "explorer",
    };

    public static IReadOnlyList<ProcessGroup> Group(
        IReadOnlyList<ProcessSample> samples,
        IReadOnlyDictionary<int, double> cpuByPid,
        string windowsDirectory,
        int currentProcessId)
    {
        var windowsPrefix = PrefixOf(windowsDirectory);
        return samples
            .GroupBy(KeyOf, StringComparer.OrdinalIgnoreCase)
            .Select(group => Build(group.Key, group.ToList(), cpuByPid, windowsPrefix, currentProcessId))
            .ToList();
    }

    /// <summary>True when a process may never be ended from Porchlight, whatever the page says.</summary>
    public static bool IsProtected(ProcessSample sample, string windowsDirectory, int currentProcessId) =>
        IsProtectedWithPrefix(sample, PrefixOf(windowsDirectory), currentProcessId);

    private static string? PrefixOf(string windowsDirectory) =>
        string.IsNullOrWhiteSpace(windowsDirectory) ? null : windowsDirectory.TrimEnd('\\') + "\\";

    private static string KeyOf(ProcessSample sample) =>
        string.IsNullOrWhiteSpace(sample.ExecutablePath) ? "name:" + sample.Name : sample.ExecutablePath;

    private static ProcessGroup Build(
        string key,
        List<ProcessSample> members,
        IReadOnlyDictionary<int, double> cpuByPid,
        string? windowsPrefix,
        int currentProcessId)
    {
        var first = members[0];
        var path = string.IsNullOrWhiteSpace(first.ExecutablePath) ? null : first.ExecutablePath;
        var inWindows = members.Any(m => IsInWindowsFolder(m.ExecutablePath, windowsPrefix))
            || (path is null && members.All(m => m.SessionId == ServicesSessionId));

        var section = inWindows
            ? RunningAppSection.Windows
            : members.Any(m => m.HasMainWindow) ? RunningAppSection.Apps : RunningAppSection.Background;

        var canEnd = section != RunningAppSection.Windows
            && members.All(m => !IsProtectedWithPrefix(m, windowsPrefix, currentProcessId));

        var cpu = Math.Min(MaxCpuPercent, members.Sum(m => cpuByPid.GetValueOrDefault(m.Pid)));
        return new ProcessGroup(key.ToLowerInvariant(), first.Name, path, section, canEnd, cpu, members.Sum(m => m.MemoryBytes), members);
    }

    private static bool IsProtectedWithPrefix(ProcessSample sample, string? windowsPrefix, int currentProcessId) =>
        sample.Pid == currentProcessId
        || sample.SessionId == ServicesSessionId
        || CriticalNames.Contains(sample.Name)
        || IsInWindowsFolder(sample.ExecutablePath, windowsPrefix);

    private static bool IsInWindowsFolder(string? path, string? windowsPrefix) =>
        windowsPrefix is not null
        && path is not null
        && path.StartsWith(windowsPrefix, StringComparison.OrdinalIgnoreCase);
}
