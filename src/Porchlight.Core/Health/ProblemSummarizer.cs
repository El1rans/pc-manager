using System.Globalization;

namespace Porchlight.Core.Health;

/// <summary>Pure mapping and grouping of Event Log records into plain-language problems - see
/// <c>docs/specs/14-system-health.md</c>.</summary>
public static class ProblemSummarizer
{
    /// <summary>How far back "recent" reaches.</summary>
    public const int WindowDays = 30;

    /// <summary>Shown when nothing was found.</summary>
    public const string NoProblemsText = "No problems found in the last 30 days.";

    private const string AppErrorProvider = "Application Error";
    private const string WerProvider = "Windows Error Reporting";
    private const string BugCheckProvider = "Microsoft-Windows-WER-SystemErrorReporting";
    private const string KernelPowerProvider = "Microsoft-Windows-Kernel-Power";
    private const string EventLogProvider = "EventLog";
    private const string DiskProvider = "disk";
    private const string NtfsProvider = "Ntfs";
    private const string UpdateProvider = "Microsoft-Windows-WindowsUpdateClient";

    private const int AppCrashId = 1000;
    private const int WerId = 1001;
    private const int BugCheckId = 1001;
    private const int KernelPowerId = 41;
    private const int DirtyShutdownId = 6008;
    private const int NtfsId = 55;
    private const int UpdateFailedId = 20;
    private const int AppNameIndex = 0;
    private const int WerEventNameIndex = 2;
    private const int WerAppNameIndex = 5;
    private const string WerAppCrashName = "APPCRASH";

    private static readonly int[] DiskIds = [7, 51, 153];

    /// <summary>A WER APPCRASH within this of an Application Error 1000 for the same app is the same crash.</summary>
    private static readonly TimeSpan CrashDuplicateWindow = TimeSpan.FromMinutes(2);

    /// <summary>Power-loss events (41 and 6008) closer together than this are one incident.</summary>
    private static readonly TimeSpan ShutdownMergeWindow = TimeSpan.FromMinutes(10);

    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Chrome",
        ["msedge"] = "Microsoft Edge",
        ["firefox"] = "Firefox",
        ["explorer"] = "Windows Explorer",
        ["winword"] = "Word",
        ["excel"] = "Excel",
        ["outlook"] = "Outlook",
        ["powerpnt"] = "PowerPoint",
    };

    public static IReadOnlyList<ProblemSummary> Summarize(IEnumerable<HealthEventRecord> records, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(records);

        var cutoff = now - TimeSpan.FromDays(WindowDays);
        var recent = records.Where(r => r.TimeCreated >= cutoff && r.TimeCreated <= now.AddDays(1)).ToList();
        var result = new List<ProblemSummary>();

        AddAppCrashes(recent, result);

        var blue = recent.Where(r => Is(r, BugCheckProvider, BugCheckId)).Select(r => r.TimeCreated).ToList();
        AddSimple(result, ProblemCategory.BlueScreen, blue,
            n => $"Windows showed a blue screen and restarted {Times(n)}",
            "If this keeps happening, ask for help.");

        var disk = recent
            .Where(r => (string.Equals(r.ProviderName, DiskProvider, StringComparison.OrdinalIgnoreCase)
                    && DiskIds.Contains(r.EventId)) || Is(r, NtfsProvider, NtfsId))
            .Select(r => r.TimeCreated).ToList();
        AddSimple(result, ProblemCategory.DiskError, disk,
            n => $"Windows had trouble reading or writing to a drive {Times(n)}",
            "Back up your files soon and check the disk health above.");

        var shutdownTimes = recent
            .Where(r => Is(r, KernelPowerProvider, KernelPowerId) || Is(r, EventLogProvider, DirtyShutdownId))
            .Select(r => r.TimeCreated).OrderBy(t => t).ToList();
        var incidents = MergeClose(shutdownTimes, ShutdownMergeWindow);
        AddSimple(result, ProblemCategory.UnexpectedShutdown, incidents,
            n => $"The PC shut down unexpectedly (power loss or a freeze) {Times(n)}",
            "If you didn't switch it off on purpose, check the power cable and ask for help if it repeats.");

        var updates = recent.Where(r => Is(r, UpdateProvider, UpdateFailedId)).Select(r => r.TimeCreated).ToList();
        AddSimple(result, ProblemCategory.FailedUpdate, updates,
            n => $"A Windows update failed to install {Times(n)}",
            "Try running Windows Update again from Settings.");

        return result
            .OrderBy(p => p.Category)
            .ThenByDescending(p => p.Count)
            .ThenBy(p => p.Title, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>"1 time", "4 times".</summary>
    public static string Times(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "time" : "times")}");

    /// <summary>"chrome.exe" -> "Chrome"; unknown apps get their name with a capital first letter.</summary>
    public static string FriendlyAppName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName.Trim());
        if (name.Length == 0)
        {
            return "An app";
        }

        return KnownApps.TryGetValue(name, out var known)
            ? known
            : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static void AddAppCrashes(List<HealthEventRecord> recent, List<ProblemSummary> result)
    {
        var crashes = new List<(string App, DateTimeOffset Time)>();
        var direct = recent.Where(r => Is(r, AppErrorProvider, AppCrashId)).ToList();
        foreach (var r in direct)
        {
            if (Prop(r, AppNameIndex) is { Length: > 0 } app)
            {
                crashes.Add((FriendlyAppName(app), r.TimeCreated));
            }
        }

        foreach (var r in recent.Where(r => Is(r, WerProvider, WerId)))
        {
            if (!string.Equals(Prop(r, WerEventNameIndex), WerAppCrashName, StringComparison.OrdinalIgnoreCase)
                || Prop(r, WerAppNameIndex) is not { Length: > 0 } app)
            {
                continue;
            }

            var friendly = FriendlyAppName(app);
            var duplicate = crashes.Any(c =>
                string.Equals(c.App, friendly, StringComparison.OrdinalIgnoreCase)
                && (c.Time - r.TimeCreated).Duration() <= CrashDuplicateWindow);
            if (!duplicate)
            {
                crashes.Add((friendly, r.TimeCreated));
            }
        }

        foreach (var group in crashes.GroupBy(c => c.App, StringComparer.OrdinalIgnoreCase))
        {
            var count = group.Count();
            result.Add(new ProblemSummary(
                ProblemCategory.AppCrash,
                $"{group.First().App} closed unexpectedly {Times(count)}",
                count,
                group.Max(c => c.Time),
                null));
        }
    }

    private static void AddSimple(
        List<ProblemSummary> result, ProblemCategory category, List<DateTimeOffset> times,
        Func<int, string> title, string advice)
    {
        if (times.Count > 0)
        {
            result.Add(new ProblemSummary(category, title(times.Count), times.Count, times.Max(), advice));
        }
    }

    /// <summary>Collapses sorted times where each is within <paramref name="window"/> of the previous into one.</summary>
    private static List<DateTimeOffset> MergeClose(List<DateTimeOffset> sorted, TimeSpan window)
    {
        var merged = new List<DateTimeOffset>();
        foreach (var t in sorted)
        {
            if (merged.Count > 0 && t - merged[^1] <= window)
            {
                merged[^1] = t;
            }
            else
            {
                merged.Add(t);
            }
        }

        return merged;
    }

    private static bool Is(HealthEventRecord r, string provider, int id) =>
        r.EventId == id && string.Equals(r.ProviderName, provider, StringComparison.OrdinalIgnoreCase);

    private static string? Prop(HealthEventRecord r, int index) =>
        index < r.Properties.Count ? r.Properties[index] : null;
}
