namespace Porchlight.Core.Lighting.Effects;

/// <inheritdoc cref="IEffectContext"/>
public sealed record EffectContext(
    DateTimeOffset UtcNow,
    double? CpuTemperatureCelsius,
    int PendingUpdateCount,
    IReadOnlyList<KeyPressEvent> RecentKeyPresses) : IEffectContext
{
    /// <summary>An empty context (at the Unix epoch): no temperature reading, no pending updates,
    /// no key presses - used by tests and as a starting point before the engine's first tick.</summary>
    public static readonly EffectContext Empty = new(DateTimeOffset.UnixEpoch, null, 0, []);
}
