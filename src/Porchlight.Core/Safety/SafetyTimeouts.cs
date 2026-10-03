namespace Porchlight.Core.Safety;

/// <summary>Named timeouts for the safety checks (nothing on this page may hang the UI).</summary>
public static class SafetyTimeouts
{
    /// <summary>Searching Windows Update for waiting updates ("Check now"). It can be slow, so this is
    /// generous, but it is never done automatically.</summary>
    public static readonly TimeSpan PendingUpdateSearch = TimeSpan.FromSeconds(90);

    /// <summary>Reading the update history (local, normally instant).</summary>
    public static readonly TimeSpan HistoryRead = TimeSpan.FromSeconds(30);
}
