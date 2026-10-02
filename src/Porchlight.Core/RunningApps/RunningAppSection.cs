namespace Porchlight.Core.RunningApps;

/// <summary>Which part of the Running apps page a group of processes is listed in.</summary>
public enum RunningAppSection
{
    /// <summary>Has at least one visible window.</summary>
    Apps,

    /// <summary>Runs without a window and is not part of Windows.</summary>
    Background,

    /// <summary>Part of Windows itself; cannot be ended here.</summary>
    Windows,
}
