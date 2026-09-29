namespace Porchlight.Core.Health;

/// <summary>Named timeouts for the health reads (nothing on this page may hang the UI).</summary>
public static class HealthTimeouts
{
    /// <summary>Per WMI query.</summary>
    public static readonly TimeSpan WmiQuery = TimeSpan.FromSeconds(10);

    /// <summary>Whole Event Log read (the last 30 days can be a lot of records).</summary>
    public static readonly TimeSpan EventLogRead = TimeSpan.FromSeconds(30);

    /// <summary>Calling <c>SystemRestore.CreateRestorePoint</c>, which can be slow while VSS snapshots.</summary>
    public static readonly TimeSpan RestorePointCreate = TimeSpan.FromMinutes(3);
}
