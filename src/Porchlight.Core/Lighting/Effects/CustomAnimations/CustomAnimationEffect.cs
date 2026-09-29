namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>
/// Plays a user-imported <see cref="CustomAnimation"/> (see docs/custom-animations.md) on any
/// layout: each LED takes the color at its normalized position in the current frame (see
/// <see cref="CustomAnimationFrame.Sample"/>), blended into the next frame for a
/// <see cref="CustomAnimationTransition.Fade"/> animation.
/// </summary>
public sealed class CustomAnimationEffect : IEffect
{
    /// <param name="animation">The animation to play.</param>
    /// <param name="speed">Playback speed; 1.0 plays every frame for exactly its own duration, 2.0
    /// twice as fast, etc. Must be positive.</param>
    public CustomAnimationEffect(CustomAnimation animation, double speed = 1.0)
    {
        ArgumentNullException.ThrowIfNull(animation);
        if (speed <= 0 || !double.IsFinite(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");
        }

        Animation = animation;
        Speed = speed;
    }

    public string Name => EffectRegistry.CustomAnimationEffectName;

    public CustomAnimation Animation { get; }

    public double Speed { get; }

    public void Render(in EffectFrame frame, Span<RgbColor> buffer)
    {
        var (current, next, blend) = Animation.PositionAt(frame.Elapsed * Speed);
        var from = Animation.Frames[current];
        var to = Animation.Frames[next];
        var points = frame.Layout.Points;

        for (var i = 0; i < points.Count && i < buffer.Length; i++)
        {
            var point = points[i];
            var color = from.Sample(point.X, point.Y);
            buffer[i] = blend > 0
                ? CustomAnimationFrame.Lerp(color, to.Sample(point.X, point.Y), blend)
                : color;
        }
    }
}
