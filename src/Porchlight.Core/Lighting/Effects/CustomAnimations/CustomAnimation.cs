namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>How a <see cref="CustomAnimation"/> moves from one frame to the next.</summary>
public enum CustomAnimationTransition
{
    /// <summary>Each frame is shown as-is for its whole duration, then the next one replaces it.</summary>
    Cut,

    /// <summary>Each frame blends smoothly into the next one over its duration.</summary>
    Fade,
}

/// <summary>
/// A user-supplied LED animation, parsed and validated from a <c>porchlight-animation</c> JSON file
/// by <see cref="CustomAnimationParser"/> - see docs/custom-animations.md for the format. Purely
/// declarative (a list of color frames), never code, so importing one can never run anything on the
/// user's PC. Immutable once built.
/// </summary>
public sealed class CustomAnimation
{
    private readonly double[] _frameStartSeconds;

    internal CustomAnimation(
        string name,
        string? description,
        string? author,
        bool loop,
        CustomAnimationTransition transition,
        IReadOnlyList<CustomAnimationFrame> frames)
    {
        Name = name;
        Description = description;
        Author = author;
        Loop = loop;
        Transition = transition;
        Frames = frames;

        _frameStartSeconds = new double[frames.Count];
        var total = 0d;
        for (var i = 0; i < frames.Count; i++)
        {
            _frameStartSeconds[i] = total;
            total += frames[i].Duration.TotalSeconds;
        }

        TotalDuration = TimeSpan.FromSeconds(total);
    }

    public string Name { get; }

    public string? Description { get; }

    public string? Author { get; }

    /// <summary>Whether the animation starts over after its last frame (the default), or holds the
    /// last frame forever.</summary>
    public bool Loop { get; }

    public CustomAnimationTransition Transition { get; }

    /// <summary>Always at least one frame.</summary>
    public IReadOnlyList<CustomAnimationFrame> Frames { get; }

    /// <summary>One full pass through every frame.</summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Which frame is showing at <paramref name="elapsed"/>, the frame it is heading towards, and
    /// how far (0..1) it has blended into that next frame - always 0 for
    /// <see cref="CustomAnimationTransition.Cut"/>, and for the held last frame of a non-looping
    /// animation.
    /// </summary>
    public (int Current, int Next, double Blend) PositionAt(TimeSpan elapsed)
    {
        var totalSeconds = TotalDuration.TotalSeconds;
        var t = Math.Max(0, elapsed.TotalSeconds);
        var last = Frames.Count - 1;

        if (Loop)
        {
            t %= totalSeconds;
        }
        else if (t >= totalSeconds)
        {
            return (last, last, 0);
        }

        // Last frame whose start is <= t.
        var index = Array.BinarySearch(_frameStartSeconds, t);
        if (index < 0)
        {
            index = ~index - 1;
        }

        index = Math.Clamp(index, 0, last);

        var next = index < last ? index + 1 : (Loop ? 0 : last);
        if (Transition == CustomAnimationTransition.Cut || next == index)
        {
            return (index, next, 0);
        }

        var blend = (t - _frameStartSeconds[index]) / Frames[index].Duration.TotalSeconds;
        return (index, next, Math.Clamp(blend, 0, 1));
    }
}
