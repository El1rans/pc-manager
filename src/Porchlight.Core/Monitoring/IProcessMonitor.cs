namespace Porchlight.Core.Monitoring;

/// <summary>Reports the top CPU/memory consuming processes, grouped by process name.</summary>
public interface IProcessMonitor
{
    /// <summary>The top <paramref name="count"/> process groups, sorted by CPU percent descending
    /// then working set descending.</summary>
    IReadOnlyList<ProcessGroupSnapshot> SampleTop(int count);
}
