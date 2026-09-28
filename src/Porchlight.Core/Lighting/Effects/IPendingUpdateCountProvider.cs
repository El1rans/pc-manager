namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// How many app updates are currently pending, for <see cref="UpdatesAlertEffect"/>. The Updates
/// feature (<c>Porchlight.Core.Winget</c>) does not currently expose a pending-update count as a
/// DI-injectable service - it is only ever computed inside the Updates page's own view model - so
/// this is a small seam the effects engine defines for itself (see docs/specs/11-led-effects.md's
/// deviations note) rather than depending on a UI-layer type. <see cref="ZeroPendingUpdateCountProvider"/>
/// is registered here in phase 1; a later change can have the Updates feature supply a real
/// implementation via DI without <see cref="EffectEngine"/> changing at all.
/// </summary>
public interface IPendingUpdateCountProvider
{
    /// <summary>How many updates are currently pending. Zero when there are none, or the real
    /// count is unknown.</summary>
    int GetPendingUpdateCount();
}
