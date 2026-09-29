using System.Globalization;
using Porchlight.Core.Lighting.Effects.CustomAnimations;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Builds an <see cref="IEffect"/> by name from an <see cref="EffectAssignment"/>'s settings - the
/// lookup <see cref="EffectEngine"/> uses to turn persisted assignments into live effect instances.
/// Every effect in docs/specs/11-led-effects.md's "Effects v1"/"Effects v2" is registered here by
/// its <see cref="IEffect.Name"/>; an unknown name or an unparsable setting falls back to that
/// effect's own constructor defaults rather than throwing, so a corrupt or stale settings file
/// never breaks the engine.
/// </summary>
public static class EffectRegistry
{
    /// <summary>Plays one of the user's imported animations (see docs/custom-animations.md), chosen
    /// by the assignment's <see cref="CustomAnimationIdSetting"/> setting.</summary>
    public const string CustomAnimationEffectName = "Custom animation";

    /// <summary>The <see cref="EffectAssignment.Settings"/> key holding a
    /// <see cref="CustomAnimationEffectName"/> assignment's <see cref="CustomAnimationInfo.Id"/>.</summary>
    public const string CustomAnimationIdSetting = "animationId";

    private static readonly IReadOnlyList<string> KnownNames =
    [
        "Rainbow wave", "Breathing", "CPU temperature", "Pac-Man", "Rain", "Typing ripple",
        CustomAnimationEffectName,
    ];

    /// <summary>Every effect name this registry can build, for a future settings UI's picker.</summary>
    public static IReadOnlyList<string> Names => KnownNames;

    /// <summary>
    /// Builds the effect named <paramref name="effectName"/> with <paramref name="settings"/>
    /// applied over its defaults. Returns null for an unknown name - callers (namely
    /// <see cref="EffectEngine"/>) treat that as "no effect assigned" rather than throwing, so a
    /// device's assignment referencing an effect from a future settings UI or a removed effect
    /// degrades to "no effect" instead of crashing. <see cref="CustomAnimationEffectName"/> also
    /// returns null when <paramref name="customAnimations"/> is not supplied or the referenced
    /// animation has since been removed.
    /// </summary>
    public static IEffect? Create(
        string effectName,
        IReadOnlyDictionary<string, string>? settings = null,
        ICustomAnimationLibrary? customAnimations = null)
    {
        ArgumentNullException.ThrowIfNull(effectName);
        var s = settings ?? new Dictionary<string, string>();

        return effectName switch
        {
            "Rainbow wave" => new RainbowWaveEffect(
                speed: GetDouble(s, "speed", 1.0),
                reverse: GetBool(s, "reverse", false)),

            "Breathing" => new BreathingEffect(
                color: GetColor(s, "color", RgbColor.White),
                speed: GetDouble(s, "speed", 1.0),
                minBrightness: GetDouble(s, "minBrightness", 0.0)),

            "CPU temperature" => new CpuTemperatureEffect(
                minC: GetDouble(s, "minC", 30),
                maxC: GetDouble(s, "maxC", 85),
                smoothingSeconds: GetDouble(s, "smoothingSeconds", 2.0)),

            "Pac-Man" => new PacManEffect(
                cellsPerSecond: GetDouble(s, "cellsPerSecond", 6.0),
                ghostLeadCells: (int)GetDouble(s, "ghostLeadCells", 2)),

            "Rain" => new RainEffect(
                rowsPerSecond: GetDouble(s, "rowsPerSecond", 8.0),
                trailLength: (int)GetDouble(s, "trailLength", 4)),

            "Typing ripple" => new TypingRippleEffect(
                ringsPerSecond: GetDouble(s, "ringsPerSecond", 6.0),
                ringWidth: GetDouble(s, "ringWidth", 1.5),
                maxAgeSeconds: GetDouble(s, "maxAgeSeconds", 1.5)),

            CustomAnimationEffectName => CreateCustomAnimation(s, customAnimations),

            _ => null,
        };
    }

    /// <summary>Wraps a built effect with <see cref="UpdatesAlertEffect"/> when
    /// <paramref name="withUpdatesAlert"/> is set - the composable overlay from
    /// docs/specs/11-led-effects.md, applied by <see cref="EffectEngine"/> per-assignment rather
    /// than being its own named effect.</summary>
    public static IEffect? CreateWithOverlay(
        string effectName,
        IReadOnlyDictionary<string, string>? settings,
        bool withUpdatesAlert,
        ICustomAnimationLibrary? customAnimations = null)
    {
        var effect = Create(effectName, settings, customAnimations);
        if (effect is null)
        {
            return null;
        }

        return withUpdatesAlert ? new UpdatesAlertEffect(effect) : effect;
    }

    private static CustomAnimationEffect? CreateCustomAnimation(
        IReadOnlyDictionary<string, string> settings, ICustomAnimationLibrary? customAnimations)
    {
        if (customAnimations is null ||
            !settings.TryGetValue(CustomAnimationIdSetting, out var id) ||
            customAnimations.Load(id) is not { } animation)
        {
            return null;
        }

        var speed = GetDouble(settings, "speed", 1.0);
        return new CustomAnimationEffect(animation, speed is > 0 and <= 10 ? speed : 1.0);
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var raw) &&
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> settings, string key, bool fallback) =>
        settings.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : fallback;

    private static RgbColor GetColor(IReadOnlyDictionary<string, string> settings, string key, RgbColor fallback) =>
        settings.TryGetValue(key, out var raw) && RgbColor.TryParse(raw, out var color) ? color : fallback;
}
