namespace Porchlight.Core.Lighting;

/// <summary>
/// Detects other software that can fight OpenRGB for control of the same RGB device (see
/// docs/specs/05-lighting.md addendum, "Lighting conflict warning"). Purely read-only: it never
/// changes a setting, stops a process, or touches OpenRGB itself.
/// </summary>
public interface ILightingConflictDetector
{
    /// <summary>
    /// Detects every currently-applicable conflict: Windows Dynamic Lighting being enabled (with a
    /// specific message when its brightness is 0, since that alone turns controlled devices off),
    /// and any known vendor RGB software that is currently running or has its service installed.
    /// Order is stable (Windows Dynamic Lighting first, then vendors in catalog order) so the UI
    /// list does not reshuffle between calls.
    /// </summary>
    Task<IReadOnlyList<LightingConflictWarning>> DetectAsync(CancellationToken cancellationToken);
}
