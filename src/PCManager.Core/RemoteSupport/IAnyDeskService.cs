namespace PCManager.Core.RemoteSupport;

/// <summary>
/// Remote support via AnyDesk. Detection, install and start are all delegated to
/// <c>IComponentService</c> (component id <c>anydesk</c> - see <c>ComponentIds.AnyDesk</c>); this
/// service adds only reading and formatting the AnyDesk address on top, per
/// docs/specs/06-remote-support.md ("Amended after 01b").
/// </summary>
public interface IAnyDeskService
{
    /// <summary>
    /// Builds the current <see cref="AnyDeskState"/>: detects AnyDesk via <c>IComponentService</c>,
    /// then - if installed - reads its ID and alias (<c>AnyDesk.exe --get-id</c> / <c>--get-alias</c>,
    /// falling back to <c>system.conf</c>/<c>service.conf</c> if the CLI call fails). Runs off the
    /// calling thread.
    /// </summary>
    Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Starts AnyDesk normally (via <c>IComponentService.StartAsync</c>), so incoming
    /// connection requests show AnyDesk's own accept dialog. Never changes any AnyDesk security
    /// setting.</summary>
    Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken);
}
