namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// A render function that turns one <see cref="EffectFrame"/> into LED colors - pure (no I/O, no
/// hardware access, no mutable state beyond an effect's own tuning parameters and any minimal
/// temporal smoothing it documents - see <see cref="CpuTemperatureEffect"/>) and deterministic for
/// a given sequence of calls, so every effect in docs/specs/11-led-effects.md is unit-testable
/// without a real device or a real clock.
/// </summary>
public interface IEffect
{
    /// <summary>Display name, used as the persisted key in
    /// <see cref="EffectAssignment.EffectName"/> and <see cref="EffectRegistry"/>'s lookup.</summary>
    string Name { get; }

    /// <summary>
    /// Renders <paramref name="frame"/> into <paramref name="buffer"/>, whose length always equals
    /// <c>frame.Layout.Count</c> - buffer index <c>i</c> is the color for <c>frame.Layout.Points[i]</c>.
    /// An effect that only makes sense on a matrix layout (see docs/specs/11-led-effects.md's
    /// "Effects v2") must check <see cref="LedLayout.IsMatrix"/> and, when false, either leave the
    /// buffer untouched or render a simple non-matrix fallback - never throw.
    /// </summary>
    void Render(in EffectFrame frame, Span<RgbColor> buffer);
}
