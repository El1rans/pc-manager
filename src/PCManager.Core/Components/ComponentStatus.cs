namespace PCManager.Core.Components;

/// <summary>Point-in-time status of one component, as reported by <see cref="IComponentService"/>.</summary>
/// <param name="State">Where the component currently stands.</param>
/// <param name="Version">Installed version, when known (from the uninstall registry entry).</param>
/// <param name="Path">Path to the component's executable, when found.</param>
/// <param name="Message">A user-readable explanation, set for <see cref="ComponentState.Error"/>
/// (e.g. "Installation was cancelled.") and left null otherwise.</param>
/// <param name="PathIsTrusted">
/// True if only an administrator could have placed <paramref name="Path"/> there: found under a
/// Program Files directory, or under an HKLM (per-machine) uninstall entry's <c>InstallLocation</c>.
/// False for a user-writable source (an HKCU per-user uninstall entry, or a user-set path override)
/// - meaningless when <paramref name="Path"/> is null. A caller that will execute
/// <paramref name="Path"/> directly (bypassing <c>IComponentService.StartAsync</c>'s own check)
/// must still refuse to do so while running elevated unless this is true - see
/// <c>AnyDeskService.BuildStateAsync</c>.
/// </param>
public sealed record ComponentStatus(
    ComponentState State,
    string? Version = null,
    string? Path = null,
    string? Message = null,
    bool PathIsTrusted = false)
{
    public static ComponentStatus NotInstalled { get; } = new(ComponentState.NotInstalled);
}
