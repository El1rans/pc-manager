namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// One device's assigned effect, as persisted in <c>Porchlight.Core.Settings.LightingSettings.EffectAssignments</c>
/// and consumed by <see cref="EffectEngine"/> / <see cref="EffectRegistry"/>. Every value is a
/// simple string so it round-trips through JSON the same way the rest of settings does, without
/// needing per-effect strongly-typed settings classes; <see cref="EffectRegistry"/> parses each
/// entry with the tolerant defaults an effect's own constructor already has.
/// </summary>
/// <param name="DeviceKey">The device's name (<c>RgbDevice.Name</c> / <c>EffectDeviceInfo.Name</c>) -
/// OpenRGB's own device index is only stable for one connection's lifetime, so name is what
/// persists across app/OpenRGB restarts.</param>
/// <param name="EffectName">The assigned effect's <see cref="IEffect.Name"/>, looked up in
/// <see cref="EffectRegistry"/>.</param>
/// <param name="Settings">Effect-specific tuning values (e.g. <c>"speed"</c>, <c>"minC"</c>,
/// <c>"color"</c> as <c>#RRGGBB</c>), by name. Missing or unparsable entries fall back to that
/// setting's default - never a reason to fail loading settings.</param>
public sealed class EffectAssignment
{
    public string DeviceKey { get; set; } = string.Empty;

    public string EffectName { get; set; } = string.Empty;

    /// <summary>Whether to wrap the assigned effect with <see cref="UpdatesAlertEffect"/> (see
    /// docs/specs/11-led-effects.md's composable overlay).</summary>
    public bool ShowUpdatesAlert { get; set; }

    public Dictionary<string, string> Settings { get; set; } = [];
}
