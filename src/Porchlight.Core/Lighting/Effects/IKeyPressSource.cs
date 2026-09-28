namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Feeds <see cref="TypingRippleEffect"/> a stream of key presses already translated to LED grid
/// positions. Interface only in phase 1 - the real, opt-in global keyboard hook implementation is
/// phase 2 (see docs/specs/11-led-effects.md). <see cref="NullKeyPressSource"/> is the phase 1
/// default: it raises nothing, so <see cref="TypingRippleEffect"/> simply renders no ripple.
/// </summary>
/// <remarks>
/// A real implementation must never expose which key was pressed, its scan code, or any character
/// - only the LED grid position it maps to (see <see cref="KeyPressEvent"/>) - and must be strictly
/// opt-in (a settings toggle, off by default) since it implies a global keyboard hook.
/// </remarks>
public interface IKeyPressSource
{
    /// <summary>Raised when a key is pressed and translated to an LED grid position.</summary>
    event EventHandler<KeyPressEvent>? KeyPressed;
}
