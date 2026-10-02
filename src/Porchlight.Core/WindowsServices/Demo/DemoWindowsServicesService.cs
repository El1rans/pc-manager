#if DEBUG
namespace Porchlight.Core.WindowsServices.Demo;

/// <summary>
/// DEBUG-only fake <see cref="IWindowsServicesService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c>): a made-up list held in memory, so screenshots never show
/// the real machine's software and changes never touch a real service.
/// </summary>
internal sealed class DemoWindowsServicesService : IWindowsServicesService
{
    private readonly object _gate = new();

    private readonly List<WindowsServiceEntry> _entries =
    [
        Make("SpotifyUpdater", "Spotify Update Service", "Keeps Spotify up to date.", "Spotify AB", ServiceStartType.Automatic, ServiceRunState.Running, false),
        Make("AdobeARMservice", "Adobe Acrobat Update Service", "Checks for Adobe Acrobat updates.", "Adobe Inc.", ServiceStartType.AutomaticDelayed, ServiceRunState.Running, false),
        Make("PhotoSyncAgent", "Photo Sync Agent", "Copies new photos to the cloud.", "Sample Software Ltd", ServiceStartType.Automatic, ServiceRunState.Stopped, false),
        Make("PrinterHelper", "Printer Helper", string.Empty, "Contoso Printers", ServiceStartType.Manual, ServiceRunState.Stopped, false),
        Make("GameLauncherSvc", "Game Launcher Service", "Installs and repairs games.", "Fabrikam Games", ServiceStartType.Disabled, ServiceRunState.Stopped, false),
        Make("Spooler", "Print Spooler", "Loads files to memory for later printing.", "Microsoft Corporation", ServiceStartType.Automatic, ServiceRunState.Running, true),
        Make("Dhcp", "DHCP Client", "Registers and updates IP addresses.", "Microsoft Corporation", ServiceStartType.Automatic, ServiceRunState.Running, true),
    ];

    public Task<IReadOnlyList<WindowsServiceEntry>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<WindowsServiceEntry>>([.. _entries]);
        }
    }

    public Task<ServiceChangeOutcome> StartAsync(string name, CancellationToken cancellationToken) =>
        Update(name, e => e with { State = ServiceRunState.Running });

    public Task<ServiceChangeOutcome> StopAsync(string name, CancellationToken cancellationToken) =>
        Update(name, e => e with { State = ServiceRunState.Stopped });

    public Task<ServiceChangeOutcome> RestartAsync(string name, CancellationToken cancellationToken) =>
        Update(name, e => e with { State = ServiceRunState.Running });

    public Task<ServiceChangeOutcome> SetStartTypeAsync(string name, ServiceStartType startType, CancellationToken cancellationToken) =>
        Update(name, e => e with { StartType = startType });

    private Task<ServiceChangeOutcome> Update(string name, Func<WindowsServiceEntry, WindowsServiceEntry> change)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(e => e.Name == name);
            if (index < 0)
            {
                return Task.FromResult(ServiceChangeOutcome.Of(ServiceChangeResult.NotFound));
            }

            if (!_entries[index].IsChangeable)
            {
                return Task.FromResult(ServiceChangeOutcome.Of(ServiceChangeResult.Refused));
            }

            _entries[index] = change(_entries[index]);
            return Task.FromResult(ServiceChangeOutcome.Of(ServiceChangeResult.Changed));
        }
    }

    private static WindowsServiceEntry Make(
        string name, string displayName, string description, string publisher,
        ServiceStartType startType, ServiceRunState state, bool microsoft) =>
        new(name, displayName, description, publisher, startType, state, microsoft, !microsoft);
}
#endif
