namespace Porchlight.Core.Lighting.Effects;

/// <summary>Everything an <see cref="IEffect"/> needs to render one frame.</summary>
/// <param name="Elapsed">Time elapsed since the effect started (or was assigned to this device) -
/// the only clock an effect should use, so it stays deterministic and unit-testable without a real
/// wall-clock delay.</param>
/// <param name="Layout">The device zone's LED layout - see <see cref="LedLayoutBuilder"/>. An
/// effect's render buffer must have exactly <see cref="LedLayout.Count"/> entries, in
/// <see cref="LedLayout.Points"/> order.</param>
/// <param name="Context">Non-visual state (CPU temperature, pending updates, recent key presses)
/// the effect can react to.</param>
public readonly record struct EffectFrame(TimeSpan Elapsed, LedLayout Layout, IEffectContext Context);
