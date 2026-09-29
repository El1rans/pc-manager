using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Spec 10: "Hide unused sensors ... hides sensors whose value has been null or 0 for
/// their whole observed history ... Never hides temperatures that report a real value."</summary>
public sealed class UnusedSensorTrackerTests
{
    private static SensorReading Reading(string id, SensorType type, double? value) =>
        new(id, "Test sensor", type, value, value, value, DateTimeOffset.UtcNow);

    [Fact]
    public void Observe_AlwaysZero_StaysUnused()
    {
        var tracker = new UnusedSensorTracker();
        for (var i = 0; i < 5; i++)
        {
            Assert.False(tracker.Observe(Reading("fan-1", SensorType.Fan, 0)));
        }
    }

    [Fact]
    public void Observe_AlwaysNull_StaysUnused()
    {
        var tracker = new UnusedSensorTracker();
        for (var i = 0; i < 5; i++)
        {
            Assert.False(tracker.Observe(Reading("fan-1", SensorType.Fan, null)));
        }
    }

    [Fact]
    public void Observe_OneRealValue_BecomesUsed_AndStaysUsedAfterwards()
    {
        var tracker = new UnusedSensorTracker();
        Assert.False(tracker.Observe(Reading("fan-1", SensorType.Fan, 0)));
        Assert.True(tracker.Observe(Reading("fan-1", SensorType.Fan, 1200)));

        // Drops back to 0 later (fan spun down) - still counts as used, since it once proved it is
        // a real, connected sensor.
        Assert.True(tracker.Observe(Reading("fan-1", SensorType.Fan, 0)));
    }

    [Fact]
    public void Observe_Voltage_ExactlyZero_IsUnused()
    {
        var tracker = new UnusedSensorTracker();
        Assert.False(tracker.Observe(Reading("voltage-7", SensorType.Voltage, 0)));
    }

    [Fact]
    public void Observe_Temperature_AnyNonNullValue_IsUsed_EvenZero()
    {
        var tracker = new UnusedSensorTracker();
        // Unlike a fan or voltage rail, a valid temperature reading of exactly 0 C still counts as
        // real - spec 10 explicitly calls this out ("never hides temperatures that report a real
        // value").
        Assert.True(tracker.Observe(Reading("temp-1", SensorType.Temperature, 0)));
    }

    [Fact]
    public void Observe_Temperature_NullValue_IsUnusedUntilARealReadingArrives()
    {
        var tracker = new UnusedSensorTracker();
        Assert.False(tracker.Observe(Reading("temp-1", SensorType.Temperature, null)));
        Assert.True(tracker.Observe(Reading("temp-1", SensorType.Temperature, 42.0)));
    }

    [Fact]
    public void Observe_DifferentSensorIds_AreTrackedIndependently()
    {
        var tracker = new UnusedSensorTracker();
        Assert.True(tracker.Observe(Reading("fan-1", SensorType.Fan, 1000)));
        Assert.False(tracker.Observe(Reading("fan-2", SensorType.Fan, 0)));
    }

    [Fact]
    public void IsEverUsed_UnknownId_ReturnsFalse()
    {
        var tracker = new UnusedSensorTracker();
        Assert.False(tracker.IsEverUsed("fan-1"));
    }

    [Fact]
    public void IsEverUsed_QueriesWithoutMutatingHistory_MatchesTheMostRecentObserve()
    {
        // Fans polish addendum: the Fans tab reads this after the sensor tree has already Observe()d
        // the same tick's RPM reading, so it must reflect that call without needing its own Observe.
        var tracker = new UnusedSensorTracker();
        tracker.Observe(Reading("fan-1", SensorType.Fan, 0));
        Assert.False(tracker.IsEverUsed("fan-1"));

        tracker.Observe(Reading("fan-1", SensorType.Fan, 1500));
        Assert.True(tracker.IsEverUsed("fan-1"));

        // Fan later idles at 0 RPM - once real, stays "used" (visible) forever, per spec 10/04.
        tracker.Observe(Reading("fan-1", SensorType.Fan, 0));
        Assert.True(tracker.IsEverUsed("fan-1"));
    }

    [Fact]
    public void PruneTo_DropsHistoryForIdsNoLongerPresent()
    {
        var tracker = new UnusedSensorTracker();
        tracker.Observe(Reading("fan-1", SensorType.Fan, 1000));
        tracker.PruneTo(new HashSet<string>());

        // After pruning, "fan-1" is a fresh id again: a single 0 reading is not (yet) enough to be
        // used.
        Assert.False(tracker.Observe(Reading("fan-1", SensorType.Fan, 0)));
    }
}
