namespace Porchlight.Core.Backup;

/// <summary>Overall answer to "is anything backing up my files?".</summary>
public enum BackupVerdict
{
    /// <summary>A recent backup exists, or OneDrive protects Documents and Desktop.</summary>
    Good,

    /// <summary>A backup is set up but out of date or only covers part of the main folders.</summary>
    Warning,

    /// <summary>Nothing is backing up the user's files.</summary>
    Problem,
}
