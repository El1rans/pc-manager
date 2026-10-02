namespace Porchlight.Core.RunningApps;

/// <summary>Reads the list of running processes. Blocking; call it off the UI thread.</summary>
public interface IProcessSnapshotSource
{
    IReadOnlyList<ProcessSample> Capture();
}
