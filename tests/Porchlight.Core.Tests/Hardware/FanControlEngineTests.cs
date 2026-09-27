using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

public sealed class FanControlEngineTests
{
    private const string FanId = "fan1";
    private const string CpuTempId = "cpu/0/temperature/0";
    private const string GpuTempId = "gpu/0/temperature/0";

    private readonly FakeClock _clock = new();

    private FanControlEngine CreateEngine() => new(_clock);

    // The curve itself is validated once, against a permissive floor of 0 - a saved curve's own
    // points do not need to match whatever the engine's runtime MinPercent happens to be right now
    // (the user could lower the curve's points before raising MinFanPercent in settings); the
    // engine's floor is applied on top, at evaluation time, regardless of how the curve was built.
    private static FanCurve CreateCurve() =>
        FanCurve.TryCreate([new(30, 30), new(50, 50), new(70, 80), new(90, 100)], minPercent: 0, out var curve, out _)
            ? curve!
            : throw new InvalidOperationException("Test curve should be valid.");

    private FanControlEngineInput CurveInput(
        double? cpuTemp,
        DateTimeOffset? cpuTempTimestamp = null,
        double minPercent = 30,
        double failsafeC = 90,
        bool enabled = true)
    {
        var cpuSample = new SensorSample(cpuTemp, cpuTempTimestamp ?? _clock.UtcNow);
        return new FanControlEngineInput(
            SoftwareControlEnabled: enabled,
            MinPercent: minPercent,
            FailsafeTemperatureC: failsafeC,
            Profiles: [new FanProfile(FanId, FanMode.Curve, SourceSensorId: CpuTempId, Curve: CreateCurve())],
            CpuGpuTemperatures: new Dictionary<string, SensorSample> { [CpuTempId] = cpuSample },
            FanSourceTemperatures: new Dictionary<string, SensorSample> { [CpuTempId] = cpuSample });
    }

    // ---------------------------------------------------------------- rule 1: minimum floor

    [Fact]
    public void Evaluate_FixedBelowFloor_IsRaisedToMinPercent()
    {
        var engine = CreateEngine();
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Fixed, FixedPercent: 5)],
            CpuGpuTemperatures: new Dictionary<string, SensorSample>(),
            FanSourceTemperatures: new Dictionary<string, SensorSample>());

        var decision = engine.Evaluate(input);

        Assert.Equal(FanTargetKind.SetPercent, decision.Targets[FanId].Kind);
        Assert.Equal(30, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_CurveBelowFloor_IsRaisedToMinPercent()
    {
        var engine = CreateEngine();
        // At 10C the curve says 30%, but the floor is 40.
        var input = CurveInput(cpuTemp: 10, minPercent: 40);

        var decision = engine.Evaluate(input);

        Assert.Equal(40, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_FixedAboveFloor_IsUnchanged()
    {
        var engine = CreateEngine();
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Fixed, FixedPercent: 65)],
            CpuGpuTemperatures: new Dictionary<string, SensorSample>(),
            FanSourceTemperatures: new Dictionary<string, SensorSample>());

        var decision = engine.Evaluate(input);

        Assert.Equal(65, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_DefaultModeFan_AlwaysRestoresDefault()
    {
        var engine = CreateEngine();
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Default)],
            CpuGpuTemperatures: new Dictionary<string, SensorSample>(),
            FanSourceTemperatures: new Dictionary<string, SensorSample>());

        var decision = engine.Evaluate(input);

        Assert.Equal(FanTargetKind.RestoreDefault, decision.Targets[FanId].Kind);
    }

    // ---------------------------------------------------------------- rule 2: overheat failsafe

    [Fact]
    public void Evaluate_CpuAtFailsafeTemperature_ForcesFanTo100()
    {
        var engine = CreateEngine();
        var input = CurveInput(cpuTemp: 90, failsafeC: 90);

        var decision = engine.Evaluate(input);

        Assert.True(decision.IsOverheatFailsafeActive);
        Assert.Equal(CpuTempId, decision.OverheatSensorId);
        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_OverheatStaysActive_UntilTenDegreesBelowThreshold()
    {
        var engine = CreateEngine();

        // Trip the failsafe.
        engine.Evaluate(CurveInput(cpuTemp: 91, failsafeC: 90));

        // Cools to just above the recovery band (90 - 10 = 80): still active.
        var stillHot = engine.Evaluate(CurveInput(cpuTemp: 80.5, failsafeC: 90));
        Assert.True(stillHot.IsOverheatFailsafeActive);
        Assert.Equal(100, stillHot.Targets[FanId].Percent);

        // Cools to exactly the recovery threshold: cleared.
        var recovered = engine.Evaluate(CurveInput(cpuTemp: 80, failsafeC: 90));
        Assert.False(recovered.IsOverheatFailsafeActive);
    }

    [Fact]
    public void Evaluate_OverheatRecovered_FanFollowsCurveAgain()
    {
        var engine = CreateEngine();
        engine.Evaluate(CurveInput(cpuTemp: 95, failsafeC: 90));

        var decision = engine.Evaluate(CurveInput(cpuTemp: 50, failsafeC: 90));

        Assert.False(decision.IsOverheatFailsafeActive);
        Assert.Equal(50, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_OverheatSensorBecomesMissing_StaysLatched()
    {
        // S1: a missing/NaN reading is not evidence of cooling - if the sensor that tripped the
        // failsafe stops reporting entirely, the failsafe must not clear just because it can no
        // longer see a value above the threshold from that sensor.
        var engine = CreateEngine();
        engine.Evaluate(CurveInput(cpuTemp: 95, failsafeC: 90));

        var decision = engine.Evaluate(CurveInput(cpuTemp: null, failsafeC: 90));

        Assert.True(decision.IsOverheatFailsafeActive);
        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_GpuAtFailsafeTemperature_AlsoTripsFailsafe()
    {
        var engine = CreateEngine();
        var cpuSample = new SensorSample(40, _clock.UtcNow);
        var gpuSample = new SensorSample(92, _clock.UtcNow);
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Curve, SourceSensorId: CpuTempId, Curve: CreateCurve())],
            CpuGpuTemperatures: new Dictionary<string, SensorSample> { [CpuTempId] = cpuSample, [GpuTempId] = gpuSample },
            FanSourceTemperatures: new Dictionary<string, SensorSample> { [CpuTempId] = cpuSample });

        var decision = engine.Evaluate(input);

        Assert.True(decision.IsOverheatFailsafeActive);
        Assert.Equal(GpuTempId, decision.OverheatSensorId);
    }

    [Fact]
    public void Evaluate_DefaultModeFan_NotForcedTo100DuringOverheat()
    {
        var engine = CreateEngine();
        var cpuSample = new SensorSample(95, _clock.UtcNow);
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Default)],
            CpuGpuTemperatures: new Dictionary<string, SensorSample> { [CpuTempId] = cpuSample },
            FanSourceTemperatures: new Dictionary<string, SensorSample>());

        var decision = engine.Evaluate(input);

        Assert.True(decision.IsOverheatFailsafeActive);
        Assert.Equal(FanTargetKind.RestoreDefault, decision.Targets[FanId].Kind);
    }

    // ---------------------------------------------------------------- rule 3: lost/NaN/stale sensor

    [Fact]
    public void Evaluate_SourceSensorMissingFromInput_ForcesFanTo100()
    {
        var engine = CreateEngine();
        var input = new FanControlEngineInput(
            SoftwareControlEnabled: true,
            MinPercent: 30,
            FailsafeTemperatureC: 90,
            Profiles: [new FanProfile(FanId, FanMode.Curve, SourceSensorId: CpuTempId, Curve: CreateCurve())],
            CpuGpuTemperatures: new Dictionary<string, SensorSample>(),
            FanSourceTemperatures: new Dictionary<string, SensorSample>());

        var decision = engine.Evaluate(input);

        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_SourceSensorValueIsNull_ForcesFanTo100()
    {
        var engine = CreateEngine();

        var decision = engine.Evaluate(CurveInput(cpuTemp: null));

        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_SourceSensorValueIsNaN_ForcesFanTo100()
    {
        var engine = CreateEngine();

        var decision = engine.Evaluate(CurveInput(cpuTemp: double.NaN));

        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_SourceSensorStale_ForcesFanTo100()
    {
        var engine = CreateEngine();
        var staleTimestamp = _clock.UtcNow;
        _clock.Advance(TimeSpan.FromSeconds(6));

        var decision = engine.Evaluate(CurveInput(cpuTemp: 50, cpuTempTimestamp: staleTimestamp));

        Assert.Equal(100, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_SourceSensorFresh_DoesNotForce100()
    {
        var engine = CreateEngine();
        var timestamp = _clock.UtcNow;
        _clock.Advance(TimeSpan.FromSeconds(4));

        var decision = engine.Evaluate(CurveInput(cpuTemp: 50, cpuTempTimestamp: timestamp));

        Assert.Equal(50, decision.Targets[FanId].Percent);
    }

    // ---------------------------------------------------------------- rule 4: set failure

    [Fact]
    public void MarkSetFailure_ForcesRestoreDefaultForEveryFan()
    {
        var engine = CreateEngine();
        engine.MarkSetFailure();

        var decision = engine.Evaluate(CurveInput(cpuTemp: 50));

        Assert.True(decision.IsDisabledDueToError);
        Assert.Equal(FanTargetKind.RestoreDefault, decision.Targets[FanId].Kind);
    }

    [Fact]
    public void Reset_AfterSetFailure_ResumesNormalEvaluation()
    {
        var engine = CreateEngine();
        engine.MarkSetFailure();

        engine.Reset();
        var decision = engine.Evaluate(CurveInput(cpuTemp: 50));

        Assert.False(decision.IsDisabledDueToError);
        Assert.Equal(FanTargetKind.SetPercent, decision.Targets[FanId].Kind);
    }

    // ---------------------------------------------------------------- master toggle off

    [Fact]
    public void Evaluate_SoftwareControlDisabled_RestoresDefaultForEveryFan()
    {
        var engine = CreateEngine();

        var decision = engine.Evaluate(CurveInput(cpuTemp: 90, enabled: false));

        Assert.Equal(FanTargetKind.RestoreDefault, decision.Targets[FanId].Kind);
    }

    // ---------------------------------------------------------------- curve hysteresis

    [Fact]
    public void Evaluate_RisingTemperature_AppliesImmediately()
    {
        var engine = CreateEngine();
        engine.Evaluate(CurveInput(cpuTemp: 30)); // 30%

        var decision = engine.Evaluate(CurveInput(cpuTemp: 50)); // 50%, rising

        Assert.Equal(50, decision.Targets[FanId].Percent);
    }

    [Fact]
    public void Evaluate_FallingTemperature_HoldsSpeedUntilThreeDegreesBelowSetPoint()
    {
        var engine = CreateEngine();
        engine.Evaluate(CurveInput(cpuTemp: 70)); // sets speed at 80% at 70C

        // Drops to 69C (only 1C below): still holds at 80%, even though the raw curve at 69C is lower.
        var stillHigh = engine.Evaluate(CurveInput(cpuTemp: 69));
        Assert.Equal(80, stillHigh.Targets[FanId].Percent);

        // Drops to 67C (3C below the set point): now allowed to fall to the curve's real value.
        var lowered = engine.Evaluate(CurveInput(cpuTemp: 67));
        Assert.Equal(CreateCurve().Evaluate(67), lowered.Targets[FanId].Percent, precision: 6);
    }

    [Fact]
    public void Evaluate_LostSensorThenRecovered_HysteresisRestartsFromCurrentValue()
    {
        var engine = CreateEngine();
        engine.Evaluate(CurveInput(cpuTemp: 70)); // sets speed at 80%

        // Sensor lost -> 100%.
        engine.Evaluate(CurveInput(cpuTemp: null));

        // Recovers at a cooler temperature; because the lost-sensor path cleared hysteresis memory,
        // this applies immediately instead of being held at the old 80%.
        var decision = engine.Evaluate(CurveInput(cpuTemp: 40));

        Assert.Equal(CreateCurve().Evaluate(40), decision.Targets[FanId].Percent, precision: 6);
    }
}
