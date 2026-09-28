namespace Porchlight.Core.Hardware;

/// <summary>
/// Resolves a fan's display name: the user's custom name (Fans tab rename, spec 04 addendum) when
/// one is set, otherwise the hardware-reported name. A small pure helper so every place a fan name
/// is shown - the Fans tab card, Sensors tab RPM rows, and the "Hottest fan" summary tile - stays
/// in agreement without duplicating the fallback rule.
/// </summary>
public static class FanNaming
{
    /// <param name="fanId">The fan's stable controller id (<see cref="IFanController.Id"/>) -
    /// never an index, which can shift when the hardware tree re-enumerates.</param>
    /// <param name="hardwareName">The hardware-reported name, used when no custom name is set.</param>
    /// <param name="customNames">Persisted custom names, keyed by <paramref name="fanId"/> (see
    /// <c>Porchlight.Core.Settings.HardwareSettings.FanDisplayNames</c>).</param>
    public static string ResolveDisplayName(string fanId, string hardwareName, IReadOnlyDictionary<string, string> customNames) =>
        customNames.TryGetValue(fanId, out var custom) && !string.IsNullOrWhiteSpace(custom) ? custom : hardwareName;
}
