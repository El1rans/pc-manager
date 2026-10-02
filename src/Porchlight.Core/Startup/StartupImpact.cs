namespace Porchlight.Core.Startup;

/// <summary>How much a startup item slows sign-in, using Task Manager's thresholds.</summary>
public enum StartupImpact
{
    /// <summary>No measurement for this item (not in Windows' startup trace, or it can't be read).</summary>
    NotMeasured,

    /// <summary>Under 300 ms of CPU and under 300 KB of disk at startup.</summary>
    Low,

    /// <summary>300 ms to 1 s of CPU, or 300 KB to 3 MB of disk.</summary>
    Medium,

    /// <summary>More than 1 s of CPU, or more than 3 MB of disk.</summary>
    High,
}
