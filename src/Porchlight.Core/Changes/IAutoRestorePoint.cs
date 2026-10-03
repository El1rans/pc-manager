namespace Porchlight.Core.Changes;

/// <summary>Makes a restore point just before a big change (a batch of app updates, a service
/// start-type change) when the setting is on and Windows allows it. It never throws and never
/// blocks the change: a skipped or failed restore point is only logged. See
/// docs/specs/34-recent-changes.md.</summary>
public interface IAutoRestorePoint
{
    /// <param name="reason">Short phrase for the restore point's name, e.g. "Porchlight: update apps".</param>
    Task<AutoRestorePointResult> EnsureAsync(string reason, CancellationToken cancellationToken);
}
