namespace Porchlight.Core.Settings;

/// <summary>First-run and general app-lifetime bookkeeping, shared across features.</summary>
public sealed class SetupSettings
{
    /// <summary>Whether the first-run experience has been completed.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Number of times the app has started. Incremented once per launch.</summary>
    public int LaunchCount { get; set; }
}
