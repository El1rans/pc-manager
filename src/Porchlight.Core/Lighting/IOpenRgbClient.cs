namespace Porchlight.Core.Lighting;

/// <summary>
/// Thin wrapper around OpenRGB.NET's socket client, narrowed to what <see cref="LightingService"/>
/// needs and expressed with Porchlight's own DTOs (<see cref="RgbDevice"/>, <see cref="RgbColor"/>)
/// so it can be faked in tests without a real socket. Not thread-safe by itself - the underlying
/// OpenRGB.NET client isn't either - <see cref="LightingService"/> is what serializes access with a
/// semaphore; implementations of this interface do not need to.
/// </summary>
public interface IOpenRgbClient : IDisposable
{
    /// <summary>True once <see cref="Connect"/> has succeeded and the socket is still open.</summary>
    bool Connected { get; }

    /// <summary>
    /// Raised when OpenRGB's own device list changes (a device was added/removed, or its state was
    /// changed by another client). Implementations only need to forward this from the underlying
    /// client - <see cref="LightingService"/> does not poll it.
    /// </summary>
    event EventHandler? DeviceListUpdated;

    /// <summary>Connects (or reconnects) to the OpenRGB SDK server. A no-op if already connected.
    /// Throws on failure - callers treat any exception here as "not connected".</summary>
    void Connect();

    /// <summary>How many devices the server currently reports - the cheapest possible round trip,
    /// used as a heartbeat to detect a connection OpenRGB has silently gone quiet on (see
    /// <see cref="LightingService"/>'s heartbeat).</summary>
    int GetControllerCount();

    /// <summary>The single device at <paramref name="deviceIndex"/>. Cheaper than
    /// <see cref="GetAllControllerData"/> when only one device is needed.</summary>
    RgbDevice GetControllerData(int deviceIndex);

    /// <summary>Every device the server currently knows about.</summary>
    IReadOnlyList<RgbDevice> GetAllControllerData();

    /// <summary>Every saved OpenRGB profile's name.</summary>
    IReadOnlyList<string> GetProfiles();

    /// <summary>Loads the given profile on the server.</summary>
    void LoadProfile(string name);

    /// <summary>
    /// Selects the given mode (by its <see cref="RgbMode.Index"/>) on the device. When
    /// <paramref name="colors"/> is given, it is sent along with the mode change - required for a
    /// <see cref="RgbColorMode.ModeSpecific"/> mode (e.g. "Static" on a device with no per-LED
    /// direct mode), whose color only takes effect this way, not through
    /// <see cref="UpdateLeds"/>.
    /// </summary>
    void SetMode(int deviceIndex, int modeIndex, IReadOnlyList<RgbColor>? colors = null);

    /// <summary>Sets every LED of the device to the given colors. <paramref name="colors"/> must
    /// have exactly as many entries as the device has LEDs. Never call this for a device with zero
    /// LEDs - OpenRGB.NET throws on an empty color array.</summary>
    void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors);
}
