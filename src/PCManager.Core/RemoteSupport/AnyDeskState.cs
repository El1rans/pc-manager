using PCManager.Core.Components;

namespace PCManager.Core.RemoteSupport;

/// <summary>Point-in-time state of AnyDesk, as reported by <see cref="IAnyDeskService"/>. Wraps the
/// shared <see cref="ComponentStatus"/> from <c>IComponentService</c> (detection, install and start
/// all go through it - see docs/specs/06-remote-support.md) with the address information only
/// <see cref="IAnyDeskService"/> knows how to read.</summary>
/// <param name="IsInstalled">Whether AnyDesk was found at all (installed or running).</param>
/// <param name="ExePath">Path to <c>AnyDesk.exe</c>, when found.</param>
/// <param name="Version">Installed version, when known.</param>
/// <param name="Id">The numeric AnyDesk ID, when it could be read; null before AnyDesk has ever
/// registered one (e.g. immediately after install, before its first start).</param>
/// <param name="Alias">The AnyDesk alias, when one has been set; null otherwise.</param>
/// <param name="IsRunning">Whether AnyDesk's background process is currently running.</param>
/// <param name="ComponentStatus">The underlying status this state was built from, for its
/// <see cref="Components.ComponentStatus.Message"/> (e.g. an error explanation).</param>
public sealed record AnyDeskState(
    bool IsInstalled,
    string? ExePath,
    string? Version,
    string? Id,
    string? Alias,
    bool IsRunning,
    ComponentStatus ComponentStatus)
{
    /// <summary>The address to show the user: the alias when one is set (AnyDesk prefers showing
    /// the alias once configured), otherwise the numeric ID; null until either is known.</summary>
    public string? Address => string.IsNullOrEmpty(Alias) ? Id : Alias;

    public bool IsError => ComponentStatus.State == ComponentState.Error;
}
