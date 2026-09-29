namespace Porchlight.Core.Health;

/// <summary>What the Restore point card shows.</summary>
/// <param name="ProtectionEnabled">Whether System Protection is on; null when it could not be determined.</param>
/// <param name="FrequencyMinutes">Minimum minutes Windows enforces between restore points
/// (<c>SystemRestorePointCreationFrequency</c>; 0 = no limit).</param>
/// <param name="Recent">Most recent restore points, newest first; null when the list could not be read
/// (it usually needs administrator rights).</param>
public sealed record RestorePointStatus(
    bool? ProtectionEnabled, int FrequencyMinutes, IReadOnlyList<RestorePointInfo>? Recent);
