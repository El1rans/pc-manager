namespace Porchlight.Core.Hardware;

/// <summary>Testing seam for anything that needs "now" - lets <see cref="FanControlEngine"/> tests
/// use a fake clock to exercise stale-sensor and hysteresis timing deterministically.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
