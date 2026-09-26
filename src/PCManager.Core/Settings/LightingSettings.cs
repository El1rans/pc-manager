namespace PCManager.Core.Settings;

/// <summary>Settings owned by the Lighting feature (populated starting with milestone 05).</summary>
public sealed class LightingSettings
{
    /// <summary>
    /// User-supplied path to <c>OpenRGB.exe</c>, for a portable install that a registry/Program
    /// Files scan would not find. Null means "detect automatically".
    /// </summary>
    public string? OpenRgbPathOverride { get; set; }

    /// <summary>Whether PC Manager should start OpenRGB (minimized, with its SDK server) if it is
    /// not already running.</summary>
    public bool AutoStartOpenRgb { get; set; }
}
