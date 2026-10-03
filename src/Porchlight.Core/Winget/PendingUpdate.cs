namespace Porchlight.Core.Winget;

/// <summary>One app update waiting at the last check, as remembered by <see cref="IPendingUpdatesTracker"/>.</summary>
/// <param name="Name">Display name.</param>
/// <param name="InstalledVersion">Currently installed version (may be <c>Unknown</c>).</param>
/// <param name="AvailableVersion">Version available.</param>
public sealed record PendingUpdate(string Name, string InstalledVersion, string AvailableVersion);
