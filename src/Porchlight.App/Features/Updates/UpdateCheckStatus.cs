namespace Porchlight.App.Features.Updates;

/// <summary>How an update check ended.</summary>
public enum UpdateCheckStatus
{
    /// <summary>winget listed the available updates.</summary>
    Succeeded,

    /// <summary>The check ran but failed (winget missing, an error).</summary>
    Failed,

    /// <summary>The check did not run to completion: busy with another check or an update run, or
    /// superseded by a newer check.</summary>
    Skipped,
}
