namespace Porchlight.Core.RunningApps;

/// <summary>Total and available physical memory.</summary>
public interface ISystemMemoryInfo
{
    /// <summary>Returns the current status, or null if Windows would not say.</summary>
    MemoryStatus? Read();
}
