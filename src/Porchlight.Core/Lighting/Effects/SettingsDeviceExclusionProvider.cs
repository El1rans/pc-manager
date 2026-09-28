using Porchlight.Core.Settings;

namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// Real <see cref="IDeviceExclusionProvider"/>: backed by the same per-device "Don't control this
/// device" list the Lighting page's own "Apply to all"/"Turn off all" honour (see
/// <c>LightingSettings.ExcludedDeviceNames</c>, docs/specs/05-lighting.md addendum "Per-device
/// exclusion"). Reads <see cref="ISettingsStore.Current"/> fresh on every call rather than caching,
/// so a device excluded (or un-excluded) while the effects engine is running is honoured without
/// restarting it - <see cref="EffectEngine"/> checks this once per device at <c>Start</c>, so the
/// engine itself still needs a Stop/Start to pick up a mid-run change, but the source of truth
/// here is always current.
/// </summary>
public sealed class SettingsDeviceExclusionProvider(ISettingsStore settingsStore) : IDeviceExclusionProvider
{
    public bool IsExcluded(string deviceName) =>
        settingsStore.Current.Lighting.ExcludedDeviceNames.Contains(deviceName, StringComparer.OrdinalIgnoreCase);
}
