namespace Porchlight.Core.RunningApps;

/// <summary>Lists running apps with their usage and ends them safely.</summary>
public interface IRunningAppsService
{
    /// <summary>Takes a new sample off the calling thread. The first call reports 0% CPU; later
    /// calls measure the time since the previous one.</summary>
    Task<RunningAppsSnapshot> SampleAsync(CancellationToken cancellationToken);

    /// <summary>Ends every process of the group, but only ones from the latest sample.</summary>
    Task<EndTaskResult> EndAsync(string groupKey, CancellationToken cancellationToken);
}
