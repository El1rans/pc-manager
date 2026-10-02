namespace Porchlight.Core.RunningApps;

/// <summary>Ends a single process. Behind an interface so kill logic is only ever tested with fakes.</summary>
public interface IProcessKiller
{
    /// <summary>
    /// Ends <paramref name="pid"/> only if it still started at <paramref name="expectedStartTime"/>
    /// (guards against pid reuse). Never kills the child processes.
    /// </summary>
    ProcessKillOutcome Kill(int pid, DateTime? expectedStartTime);
}
