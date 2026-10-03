namespace Porchlight.Core.Backup;

/// <summary>What Windows File History reports.</summary>
/// <param name="IsConfigured">A backup drive was chosen at some point.</param>
/// <param name="IsEnabled">File History is turned on (only meaningful when configured).</param>
/// <param name="LastBackup">When the last backup completed, or null if it never ran / is unknown.</param>
public sealed record FileHistoryStatus(bool IsConfigured, bool IsEnabled, DateTimeOffset? LastBackup)
{
    /// <summary>File History has never been set up.</summary>
    public static FileHistoryStatus NotSetUp { get; } = new(false, false, null);
}
