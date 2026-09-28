namespace Porchlight.App.Features.Updates;

/// <summary>Live state of one row in the Updates page's DataGrid (its Status column).</summary>
public enum UpdateRowState
{
    /// <summary>Not currently part of an update run.</summary>
    None,

    /// <summary>Selected for the update run that is about to start / has queued but not reached
    /// this package yet.</summary>
    Queued,

    /// <summary><c>winget upgrade</c> is running for this package right now.</summary>
    Updating,

    /// <summary>Updated successfully (possibly needing a restart to finish).</summary>
    Updated,

    /// <summary>The upgrade failed.</summary>
    Failed,

    /// <summary>Either winget reported no applicable update, or "Stop after current" skipped this
    /// row before it was reached.</summary>
    Skipped,
}
