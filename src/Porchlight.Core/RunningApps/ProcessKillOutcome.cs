namespace Porchlight.Core.RunningApps;

/// <summary>Result of trying to kill one process.</summary>
public enum ProcessKillOutcome
{
    Killed,

    /// <summary>The process was already gone.</summary>
    AlreadyExited,

    /// <summary>The pid now belongs to a different process (reused); nothing was killed.</summary>
    StartTimeMismatch,

    AccessDenied,

    Failed,
}
