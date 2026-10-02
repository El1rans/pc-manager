namespace Porchlight.Core.RunningApps;

/// <summary>One running process at one moment. Every field except the pid and name is best effort.</summary>
/// <param name="Pid">Process id.</param>
/// <param name="Name">Process name without ".exe".</param>
/// <param name="ExecutablePath">Full path of the program, or null when it cannot be read.</param>
/// <param name="SessionId">Windows session; 0 is the services session.</param>
/// <param name="HasMainWindow">True when the process owns a visible top-level window.</param>
/// <param name="StartTime">When the process started (used to tell a reused pid from the original), or null.</param>
/// <param name="TotalProcessorTime">CPU time used so far.</param>
/// <param name="MemoryBytes">Working set in bytes.</param>
public sealed record ProcessSample(
    int Pid,
    string Name,
    string? ExecutablePath,
    int SessionId,
    bool HasMainWindow,
    DateTime? StartTime,
    TimeSpan TotalProcessorTime,
    long MemoryBytes);
