namespace Porchlight.Core.Lighting;

/// <summary>
/// Decides whether a color-apply call may go out now, so <c>ColorWheelPicker</c> dragging does not
/// flood the OpenRGB SDK connection with a call per mouse-move event. Pure decision logic (no
/// timer, no I/O) so it is unit testable with a fake clock; the App layer owns actually calling
/// <c>ILightingService</c> and always sending the final value on release regardless of this
/// limiter (see <c>LightingViewModel</c>/<c>DeviceRowViewModel</c>). See docs/specs/05-lighting.md
/// addendum, "Color wheel picker".
/// </summary>
public sealed class ColorApplyRateLimiter
{
    /// <summary>Default minimum gap between sends: ~20 calls/second, matching the spec.</summary>
    public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(50);

    private readonly TimeSpan _minInterval;
    private readonly Func<DateTime> _utcNowProvider;
    private DateTime? _lastAcquiredUtc;

    public ColorApplyRateLimiter(TimeSpan? minInterval = null, Func<DateTime>? utcNowProvider = null)
    {
        _minInterval = minInterval ?? DefaultMinInterval;
        if (_minInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minInterval), _minInterval, "Must be a positive interval.");
        }

        _utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// True if a call may be sent right now - and if so, records that it was, so the next call
    /// within <c>minInterval</c> is refused. A refused call is simply dropped by the caller (the
    /// next drag event, moments later, will very likely be allowed); there is no queued retry
    /// here, since a superseded intermediate color while dragging is not worth sending late.
    /// </summary>
    public bool TryAcquire()
    {
        var now = _utcNowProvider();
        if (_lastAcquiredUtc is { } last && now - last < _minInterval)
        {
            return false;
        }

        _lastAcquiredUtc = now;
        return true;
    }

    /// <summary>Clears the last-sent timestamp, e.g. after a forced "final value on release" send
    /// that bypassed <see cref="TryAcquire"/>, so the next drag's first move is not itself refused.</summary>
    public void Reset() => _lastAcquiredUtc = null;
}
