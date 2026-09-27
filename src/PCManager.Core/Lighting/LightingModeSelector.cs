namespace PCManager.Core.Lighting;

/// <summary>
/// Picks which mode a device should be switched to before setting a direct color, mirroring what
/// OpenRGB's own UI does: prefer "Direct" (per-LED colors, no effect), then "Static" (one color,
/// no effect), else leave the device on whatever mode is already active.
/// </summary>
public static class LightingModeSelector
{
    private const string DirectModeName = "Direct";
    private const string StaticModeName = "Static";

    /// <summary>Returns the mode <see cref="LightingService"/> should select before writing a
    /// color, or null if the device should be left on its current mode (neither "Direct" nor
    /// "Static" is available).</summary>
    public static RgbMode? SelectColorMode(IReadOnlyList<RgbMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);

        return FindByName(modes, DirectModeName) ?? FindByName(modes, StaticModeName);
    }

    private static RgbMode? FindByName(IReadOnlyList<RgbMode> modes, string name)
    {
        foreach (var mode in modes)
        {
            if (string.Equals(mode.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }

        return null;
    }
}
