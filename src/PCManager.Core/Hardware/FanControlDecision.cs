namespace PCManager.Core.Hardware;

/// <summary>Result of one <see cref="FanControlEngine.Evaluate"/> call.</summary>
/// <param name="Targets">Per-fan outcome for every fan in the input's profile list.</param>
/// <param name="IsOverheatFailsafeActive">Rule 2: at least one CPU/GPU temperature reached the
/// failsafe threshold and recovery has not happened yet.</param>
/// <param name="OverheatSensorId">Which sensor tripped/is holding the overheat failsafe, for the
/// critical banner ("which rule, which sensor").</param>
/// <param name="IsDisabledDueToError">Rule 4: a previous <c>SetPercent</c> failure disabled software
/// control; every fan is forced to <see cref="FanTargetKind.RestoreDefault"/> until re-armed.</param>
public sealed record FanControlDecision(
    IReadOnlyDictionary<string, FanTarget> Targets,
    bool IsOverheatFailsafeActive,
    string? OverheatSensorId,
    bool IsDisabledDueToError)
{
    public static FanControlDecision Empty { get; } =
        new(new Dictionary<string, FanTarget>(), false, null, false);
}

/// <summary>What the engine wants for one fan.</summary>
/// <param name="Kind">Whether/why this fan should be under software control right now.</param>
/// <param name="Percent">Target duty cycle when <paramref name="Kind"/> calls for software control;
/// meaningless (0) for <see cref="FanTargetKind.RestoreDefault"/>.</param>
public sealed record FanTarget(FanTargetKind Kind, double Percent)
{
    public static FanTarget RestoreDefault { get; } = new(FanTargetKind.RestoreDefault, 0);

    public static FanTarget SetPercent(double percent) => new(FanTargetKind.SetPercent, percent);
}

public enum FanTargetKind
{
    /// <summary>Hand this fan back to BIOS/EC control (Default mode, control disabled, or the
    /// engine is disabled due to a previous set failure).</summary>
    RestoreDefault,

    /// <summary>Call <see cref="IFanController.SetPercent"/> with <see cref="FanTarget.Percent"/>.</summary>
    SetPercent,
}
