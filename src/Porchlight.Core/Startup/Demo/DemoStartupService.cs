#if DEBUG
namespace Porchlight.Core.Startup.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IStartupService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c>): a made-up list held in memory, so a documentation
/// screenshot never shows the real machine's software and toggling never touches the registry.
/// </summary>
internal sealed class DemoStartupService : IStartupService
{
    private readonly object _gate = new();

    private readonly List<StartupEntry> _entries =
    [
        Make(StartupSource.MachineRun, "SecurityHealth", "Windows Security notification icon", "Microsoft Corporation", true, true, StartupImpact.Low),
        Make(StartupSource.CurrentUserRun, "AnyDesk", "AnyDesk", "philandro Software GmbH", true, true, StartupImpact.Medium),
        Make(StartupSource.CurrentUserRun, "OneDrive", "Microsoft OneDrive", "Microsoft Corporation", false, true, StartupImpact.High),
        Make(StartupSource.CurrentUserRun, "Spotify", "Spotify", "Spotify AB", false, true, StartupImpact.High),
        Make(StartupSource.MachineRun32, "AdobeGCInvoker", "Adobe Genuine Software Integrity", "Adobe Inc.", false, true, StartupImpact.Low),
        Make(StartupSource.CurrentUserFolder, "Photo Frame.lnk", "Photo Frame", null, false, false),
        Make(StartupSource.LogonTask, @"\Backup Sync at logon", "Backup Sync", "Example Software Ltd", false, true, StartupImpact.Medium),
        Make(StartupSource.LogonTask, @"\Microsoft\Office\Office Background Task Handler", "Microsoft Office background tasks", "Microsoft Corporation", true, false, StartupImpact.NotMeasured),
    ];

    public bool ImpactNeedsAdmin => false;

    public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StartupEntry>>([.. _entries]);
        }
    }

    public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(e => e.Id == entryId);
            if (index < 0)
            {
                return Task.FromResult(StartupChangeResult.NotFound);
            }

            _entries[index] = _entries[index] with { IsEnabled = enabled };
            return Task.FromResult(StartupChangeResult.Changed);
        }
    }

    private static StartupEntry Make(
        StartupSource source, string itemName, string displayName, string? publisher, bool keep, bool enabled,
        StartupImpact impact = StartupImpact.NotMeasured) =>
        new($"{source}|{itemName}", source, itemName, displayName, publisher, null,
            keep ? "Part of Windows or a tool Porchlight sets up. It's best to leave this on."
                 : "Starts by itself when you sign in to Windows. Turning it off doesn't remove the program.",
            keep, enabled, impact);
}
#endif
