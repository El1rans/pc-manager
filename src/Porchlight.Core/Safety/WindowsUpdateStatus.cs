namespace Porchlight.Core.Safety;

/// <summary>Result of the Windows Update card (no search for waiting updates - see
/// <see cref="IWindowsUpdateStatusService.CheckPendingAsync"/>).</summary>
/// <param name="Verdict">One plain line.</param>
/// <param name="Level">Good / Attention / Unknown.</param>
/// <param name="LastInstalled">When Windows last installed updates, if known.</param>
/// <param name="RecentFailures">Failed attempts in the last 30 days.</param>
/// <param name="RebootPending">True when Windows is waiting for a restart; null when unknown.</param>
/// <param name="Details">Short supporting lines.</param>
public sealed record WindowsUpdateStatus(
    string Verdict,
    SafetyLevel Level,
    DateTimeOffset? LastInstalled,
    int RecentFailures,
    bool? RebootPending,
    IReadOnlyList<string> Details);
