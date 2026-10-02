#if DEBUG
namespace Porchlight.Core.Winget.Demo;

/// <summary>DEBUG-only in-memory <see cref="IUpdateHistoryStore"/> for the demo data mode: a made-up
/// week of history so a screenshot never shows (or touches) the real file.</summary>
internal sealed class FakeUpdateHistoryStore : IUpdateHistoryStore
{
    private readonly List<UpdateHistoryEntry> _entries;

    public FakeUpdateHistoryStore()
        : this(DateTimeOffset.UtcNow)
    {
    }

    internal FakeUpdateHistoryStore(DateTimeOffset now)
    {
        _entries =
        [
            Entry(now, -6.2, "Demo.NotesApp", "Demo Notes App", "3.0.0", "3.1.0", UpdateHistoryAction.Update),
            Entry(now, -5.1, "Demo.PhotoViewer", "Demo Photo Viewer", "4.1.0", "4.2.0", UpdateHistoryAction.Update),
            Entry(now, -4.4, "Demo.Archiver", "Demo Archiver", "6.24.0", "7.23.0", UpdateHistoryAction.Update,
                "Not available for this PC", "A newer version exists but doesn't apply to this PC. Reinstalling may help."),
            Entry(now, -3.3, "Demo.Archiver", "Demo Archiver", "6.24.0", "7.23.0", UpdateHistoryAction.Reinstall),
            Entry(now, -2.5, "Demo.VideoCall", "Demo Video Call", null, "9.0.0", UpdateHistoryAction.Update),
            Entry(now, -1.6, "Demo.ChatClient", "Demo Chat Client", "1.0.0", "1.0.1", UpdateHistoryAction.Update,
                "Close the app and try again", "Demo Chat Client is running. Close it, then try again."),
            Entry(now, -1.5, "Demo.ChatClient", "Demo Chat Client", "1.0.0", "1.0.1", UpdateHistoryAction.Update),
            Entry(now, -0.9, "Demo.ShellPrompt", "Demo Shell Prompt", "24.8.0", "31.3.0", UpdateHistoryAction.Update,
                "Needs a reinstall", "This update needs the app to be reinstalled."),
            Entry(now, -0.4, "Demo.Browser", "Demo Browser", "119.0.2", "120.0.1", UpdateHistoryAction.Update),
            Entry(now, -0.1, "Demo.OfficeSuite", "Demo Office Suite", "2023.10", "2023.11", UpdateHistoryAction.Update),
        ];
    }

    public IReadOnlyList<UpdateHistoryEntry> GetAll() => [.. _entries];

    public void Add(UpdateHistoryEntry entry) => _entries.Add(entry);

    public void Clear() => _entries.Clear();

    private static UpdateHistoryEntry Entry(
        DateTimeOffset now, double daysAgo, string id, string name, string? from, string? to,
        UpdateHistoryAction action, string? failureTitle = null, string? failureExplanation = null) =>
        new()
        {
            TimestampUtc = now.AddDays(daysAgo),
            PackageId = id,
            PackageName = name,
            FromVersion = from,
            ToVersion = to,
            Action = action,
            Succeeded = failureTitle is null,
            OutcomeTitle = failureTitle ?? "Updated",
            Explanation = failureExplanation ?? string.Empty,
            ExitCode = failureTitle is null ? 0 : 1,
        };
}
#endif
