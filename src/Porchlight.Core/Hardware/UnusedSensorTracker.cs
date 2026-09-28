namespace Porchlight.Core.Hardware;

/// <summary>
/// Tracks, per sensor id, whether a sensor has ever reported a "real" reading - the rule behind the
/// Hardware page's "Hide unused sensors" toggle (spec 10): a sensor whose value has been null or
/// exactly zero for its <em>entire</em> observed history is unused (an unconnected fan header, a 0 V
/// rail that isn't populated on this board). Once a sensor reports one real value it is never
/// considered unused again, even if it later reads back 0 (e.g. a fan that was spun down).
/// Temperatures are exempt from the "not exactly zero" check - any non-null temperature reading
/// counts as real, since 0 C is a plausible (if unlikely) real reading and a missing temperature
/// sensor already never appears in the LHM tree at all.
/// </summary>
public sealed class UnusedSensorTracker
{
    private readonly Dictionary<string, bool> _everReal = [];

    /// <summary>Records this tick's reading and returns whether the sensor is "used" (has ever had a
    /// real reading, counting this one).</summary>
    public bool Observe(SensorReading reading)
    {
        if (_everReal.TryGetValue(reading.Id, out var alreadyReal) && alreadyReal)
        {
            return true;
        }

        var isReal = IsRealValue(reading);
        _everReal[reading.Id] = isReal;
        return isReal;
    }

    /// <summary>Whether <paramref name="reading"/>'s current value alone counts as "real", ignoring
    /// any prior history.</summary>
    public static bool IsRealValue(SensorReading reading) =>
        reading.Type == SensorType.Temperature
            ? reading.Value is not null
            : reading.Value is not null && reading.Value.Value != 0;

    /// <summary>Drops all remembered history for ids no longer present, so a hardware id that
    /// disappears (e.g. the tree re-initializes) does not leak memory forever.</summary>
    public void PruneTo(IReadOnlyCollection<string> liveIds)
    {
        if (_everReal.Count == 0)
        {
            return;
        }

        var stale = _everReal.Keys.Where(id => !liveIds.Contains(id)).ToList();
        foreach (var id in stale)
        {
            _everReal.Remove(id);
        }
    }
}
