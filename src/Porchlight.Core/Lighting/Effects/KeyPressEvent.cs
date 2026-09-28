namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// One key press translated to an LED grid position - never the key itself. See
/// <see cref="IKeyPressSource"/> and docs/specs/11-led-effects.md's phase 2 privacy note: a real
/// implementation must never carry the pressed key, scan code, or character - only where on the
/// device's LED grid to light up.
/// </summary>
/// <param name="Col">Zero-based column on the matrix layout the press maps to.</param>
/// <param name="Row">Zero-based row on the matrix layout the press maps to.</param>
/// <param name="Timestamp">When the press happened, from the same <see cref="TimeProvider"/>
/// <see cref="EffectEngine"/> uses for frame timing.</param>
public sealed record KeyPressEvent(int Col, int Row, DateTimeOffset Timestamp);
