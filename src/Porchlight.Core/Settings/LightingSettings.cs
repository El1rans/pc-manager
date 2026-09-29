using System.Net;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Lighting.Effects;

namespace Porchlight.Core.Settings;

/// <summary>Settings owned by the Lighting feature (populated starting with milestone 05).</summary>
public sealed class LightingSettings
{
    /// <summary>
    /// User-supplied path to <c>OpenRGB.exe</c>, for a portable install that a registry/Program
    /// Files scan would not find. Null means "detect automatically".
    /// </summary>
    public string? OpenRgbPathOverride { get; set; }

    /// <summary>Whether Porchlight should start OpenRGB (minimized, with its SDK server) if it is
    /// not already running.</summary>
    public bool AutoStartOpenRgb { get; set; }

    /// <summary>OpenRGB SDK server host. Defaults to loopback - the server almost always runs on
    /// the same machine as Porchlight.</summary>
    public string OpenRgbHost { get; set; } = DefaultOpenRgbHost;

    /// <summary>Host the OpenRGB SDK server is expected on when the setting is missing or rejected.</summary>
    public const string DefaultOpenRgbHost = "127.0.0.1";

    /// <summary>
    /// The host to actually connect to. Porchlight runs elevated and settings.json is user-editable,
    /// so only loopback ("localhost" or a loopback IP literal) is honoured; anything else falls back
    /// to <see cref="DefaultOpenRgbHost"/> (with a warning when a logger is supplied).
    /// </summary>
    public string ResolveOpenRgbHost(ILogger? logger = null)
    {
        var host = OpenRgbHost?.Trim();
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)))
        {
            return host!;
        }

        logger?.LogWarning(
            "Ignoring non-loopback OpenRGB host '{Host}'; using {Default} instead.", OpenRgbHost, DefaultOpenRgbHost);
        return DefaultOpenRgbHost;
    }

    /// <summary>OpenRGB SDK server port (OpenRGB's own default is 6742).</summary>
    public int OpenRgbPort { get; set; } = 6742;

    /// <summary>Up to 8 saved favorite colors, each as a <c>#RRGGBB</c> string, oldest first.</summary>
    public List<string> FavoriteColors { get; set; } = [];

    /// <summary>
    /// Names (<see cref="Porchlight.Core.Lighting.RgbDevice.Name"/>) of devices the user marked
    /// "Don't control this device" on the Lighting page - e.g. a keyboard they'd rather leave to its
    /// vendor's own software (see docs/specs/05-lighting.md addendum, "Per-device exclusion").
    /// "Apply to all"/"Turn off all" skip these; the device still appears in the list and can still
    /// be set individually.
    /// </summary>
    public List<string> ExcludedDeviceNames { get; set; } = [];

    /// <summary>Per-device LED effect assignments for the effects engine (see
    /// docs/specs/11-led-effects.md), keyed by device name inside each entry. Empty means no device
    /// has an effect assigned - the engine (started by the phase 2 UI) then does nothing.</summary>
    public List<EffectAssignment> EffectAssignments { get; set; } = [];
}
