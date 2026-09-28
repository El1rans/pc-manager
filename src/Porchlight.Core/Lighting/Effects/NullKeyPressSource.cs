namespace Porchlight.Core.Lighting.Effects;

/// <summary>Phase 1 default <see cref="IKeyPressSource"/>: never raises <see cref="KeyPressed"/>.
/// Registered until a phase 2, opt-in global keyboard hook implementation replaces it.</summary>
public sealed class NullKeyPressSource : IKeyPressSource
{
#pragma warning disable CS0067 // Never raised - see class remarks.
    public event EventHandler<KeyPressEvent>? KeyPressed;
#pragma warning restore CS0067
}
