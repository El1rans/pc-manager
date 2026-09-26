namespace PCManager.Core.Hardware;

/// <summary>
/// Pure decision logic for fan control - spec 04's rules 1-4 (rule 5, restoring on exit/suspend,
/// is orchestration and lives in <c>FanControlManager</c> instead, since it is not a per-tick
/// decision). No LHM types anywhere in this file; fully unit testable with fake sensors and a fake
/// <see cref="IClock"/>. Stateful (curve hysteresis and the sticky overheat failsafe both need
/// memory across calls) but every state transition is a deterministic function of the inputs given
/// to <see cref="Evaluate"/>, so tests can drive it tick by tick.
/// </summary>
public sealed class FanControlEngine
{
    private readonly IClock _clock;
    private readonly Dictionary<string, FanRuntimeState> _fanStates = new();
    private bool _overheatActive;
    private string? _overheatSensorId;
    private bool _disabledDueToError;

    public FanControlEngine(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>Rule 4: true once a <c>SetPercent</c> failure has been reported via
    /// <see cref="MarkSetFailure"/>. Sticky until <see cref="Reset"/>.</summary>
    public bool IsDisabledDueToError => _disabledDueToError;

    /// <summary>
    /// Rule 4: the orchestrator calls this when calling <see cref="IFanController.SetPercent"/>
    /// throws. From this point on, every <see cref="Evaluate"/> call returns
    /// <see cref="FanTargetKind.RestoreDefault"/> for every fan, regardless of profiles or
    /// temperatures, until <see cref="Reset"/> is called.
    /// </summary>
    public void MarkSetFailure() => _disabledDueToError = true;

    /// <summary>Clears the rule-4 disabled state and all sticky/hysteresis memory. Called when the
    /// user re-enables software fan control after a failure (or simply turns it off and on again),
    /// so a fresh start does not carry over stale "last applied" state.</summary>
    public void Reset()
    {
        _disabledDueToError = false;
        _overheatActive = false;
        _overheatSensorId = null;
        _fanStates.Clear();
    }

    public FanControlDecision Evaluate(FanControlEngineInput input)
    {
        if (!input.SoftwareControlEnabled || _disabledDueToError)
        {
            var restoreAll = input.Profiles.ToDictionary(p => p.FanId, _ => FanTarget.RestoreDefault);
            return new FanControlDecision(restoreAll, _overheatActive, _overheatSensorId, _disabledDueToError);
        }

        UpdateOverheatState(input);

        var targets = new Dictionary<string, FanTarget>(input.Profiles.Count);
        foreach (var profile in input.Profiles)
        {
            targets[profile.FanId] = EvaluateFan(profile, input);
        }

        return new FanControlDecision(targets, _overheatActive, _overheatSensorId, _disabledDueToError);
    }

    /// <summary>Rule 2. Sticky: once tripped, stays active until every currently-known CPU/GPU
    /// temperature is at least <see cref="FanControlOptions.OverheatRecoveryBandC"/> below the
    /// threshold. A missing (null) reading never triggers and never blocks recovery on its own -
    /// a fan relying on a lost sensor is covered separately by rule 3.</summary>
    private void UpdateOverheatState(FanControlEngineInput input)
    {
        var recoveryThreshold = input.FailsafeTemperatureC - FanControlOptions.OverheatRecoveryBandC;

        if (!_overheatActive)
        {
            var trip = input.CpuGpuTemperatures
                .FirstOrDefault(kv => kv.Value.ValueC is { } v && v >= input.FailsafeTemperatureC);
            if (trip.Key is not null)
            {
                _overheatActive = true;
                _overheatSensorId = trip.Key;
            }

            return;
        }

        var stillHot = input.CpuGpuTemperatures
            .FirstOrDefault(kv => kv.Value.ValueC is { } v && v > recoveryThreshold);
        if (stillHot.Key is null)
        {
            _overheatActive = false;
            _overheatSensorId = null;
        }
        else
        {
            _overheatSensorId = stillHot.Key;
        }
    }

    private FanTarget EvaluateFan(FanProfile profile, FanControlEngineInput input)
    {
        if (profile.Mode == FanMode.Default)
        {
            return FanTarget.RestoreDefault;
        }

        if (_overheatActive)
        {
            return FanTarget.SetPercent(100);
        }

        if (profile.Mode == FanMode.Fixed)
        {
            var percent = Math.Min(Math.Max(profile.FixedPercent, input.MinPercent), 100);
            return FanTarget.SetPercent(percent);
        }

        return EvaluateCurveFan(profile, input);
    }

    private FanTarget EvaluateCurveFan(FanProfile profile, FanControlEngineInput input)
    {
        if (profile.SourceSensorId is null || profile.Curve is null)
        {
            _fanStates.Remove(profile.FanId);
            return FanTarget.SetPercent(100);
        }

        if (!input.FanSourceTemperatures.TryGetValue(profile.SourceSensorId, out var sample) ||
            sample.ValueC is not { } temperatureC ||
            double.IsNaN(temperatureC) ||
            _clock.UtcNow - sample.TimestampUtc > FanControlOptions.StaleSensorThreshold)
        {
            // Rule 3. Drop hysteresis memory so recovery starts from the curve's real value
            // instead of "wherever it was left before the sensor was lost".
            _fanStates.Remove(profile.FanId);
            return FanTarget.SetPercent(100);
        }

        var raw = profile.Curve.Evaluate(temperatureC);
        var withHysteresis = ApplyHysteresis(profile.FanId, temperatureC, raw);
        var floored = Math.Max(withHysteresis, input.MinPercent);
        return FanTarget.SetPercent(floored);
    }

    /// <summary>A curve only lowers speed once the temperature has dropped at least
    /// <see cref="FanControlOptions.HysteresisBandC"/> below the point where the current speed was
    /// set; a rise always applies immediately.</summary>
    private double ApplyHysteresis(string fanId, double temperatureC, double rawPercent)
    {
        if (!_fanStates.TryGetValue(fanId, out var state))
        {
            _fanStates[fanId] = new FanRuntimeState(temperatureC, rawPercent);
            return rawPercent;
        }

        if (rawPercent >= state.LastAppliedPercent ||
            temperatureC <= state.LastAppliedAtTempC - FanControlOptions.HysteresisBandC)
        {
            _fanStates[fanId] = new FanRuntimeState(temperatureC, rawPercent);
            return rawPercent;
        }

        return state.LastAppliedPercent;
    }

    private sealed record FanRuntimeState(double LastAppliedAtTempC, double LastAppliedPercent);
}
