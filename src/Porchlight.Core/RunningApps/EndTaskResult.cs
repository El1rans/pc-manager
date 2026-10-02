namespace Porchlight.Core.RunningApps;

/// <summary>Outcome of <see cref="IRunningAppsService.EndAsync"/>.</summary>
public enum EndTaskResult
{
    /// <summary>Ended (or it had already exited).</summary>
    Ended,

    /// <summary>No such app in the latest list.</summary>
    NotFound,

    /// <summary>Refused: Windows, critical or Porchlight itself.</summary>
    Refused,

    /// <summary>Windows denied access; administrator rights are needed.</summary>
    NeedsAdmin,

    /// <summary>Something else went wrong.</summary>
    Failed,
}
