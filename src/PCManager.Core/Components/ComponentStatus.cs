namespace PCManager.Core.Components;

/// <summary>Point-in-time status of one component, as reported by <see cref="IComponentService"/>.</summary>
/// <param name="State">Where the component currently stands.</param>
/// <param name="Version">Installed version, when known (from the uninstall registry entry).</param>
/// <param name="Path">Path to the component's executable, when found.</param>
/// <param name="Message">A user-readable explanation, set for <see cref="ComponentState.Error"/>
/// (e.g. "Installation was cancelled.") and left null otherwise.</param>
public sealed record ComponentStatus(ComponentState State, string? Version = null, string? Path = null, string? Message = null)
{
    public static ComponentStatus NotInstalled { get; } = new(ComponentState.NotInstalled);
}
