namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// The non-visual state an <see cref="IEffect"/> can react to, gathered fresh by
/// <see cref="EffectEngine"/> once per frame - see <see cref="EffectFrame"/>.
/// </summary>
public interface IEffectContext
{
    /// <summary>Wall-clock time this context was built for - the reference point
    /// <see cref="TypingRippleEffect"/> measures each <see cref="KeyPressEvent.Timestamp"/>'s age
    /// against. Not used for anything else; every other effect uses <see cref="EffectFrame.Elapsed"/>
    /// instead so it stays a pure function of "time since this effect started".</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>The current CPU temperature in Celsius, or null when
    /// <c>Porchlight.Core.Hardware.IHardwareService</c> has no live reading yet (hardware
    /// monitoring not started, or no CPU temperature sensor found) - see
    /// <see cref="CpuTemperatureEffect"/>'s neutral fallback.</summary>
    double? CpuTemperatureCelsius { get; }

    /// <summary>How many app updates are currently pending, from
    /// <see cref="IPendingUpdateCountProvider"/>. Zero when there are none, or the real count is
    /// unknown (see <see cref="ZeroPendingUpdateCountProvider"/>).</summary>
    int PendingUpdateCount { get; }

    /// <summary>Key presses translated to LED grid positions, most recent first, for
    /// <see cref="TypingRippleEffect"/>. Always empty unless a phase 2 <see cref="IKeyPressSource"/>
    /// is wired up and the user has opted in - see <see cref="IKeyPressSource"/>.</summary>
    IReadOnlyList<KeyPressEvent> RecentKeyPresses { get; }
}
