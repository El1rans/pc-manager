namespace PCManager.Core.Lighting;

/// <summary>
/// Thin wrapper around OpenRGB.NET's socket client, narrowed to what <see cref="LightingService"/>
/// needs and expressed with PC Manager's own DTOs (<see cref="RgbDevice"/>, <see cref="RgbColor"/>)
/// so it can be faked in tests without a real socket. Not thread-safe by itself - the underlying
/// OpenRGB.NET client isn't either - <see cref="LightingService"/> is what serializes access with a
/// semaphore; implementations of this interface do not need to.
/// </summary>
public interface IOpenRgbClient : IDisposable
{
    /// <summary>True once <see cref="Connect"/> has succeeded and the socket is still open.</summary>
    bool Connected { get; }

    /// <summary>Connects (or reconnects) to the OpenRGB SDK server. Throws on failure - callers
    /// treat any exception here as "not connected".</summary>
    void Connect();

    /// <summary>Every device the server currently knows about.</summary>
    IReadOnlyList<RgbDevice> GetAllControllerData();

    /// <summary>Every saved OpenRGB profile's name.</summary>
    IReadOnlyList<string> GetProfiles();

    /// <summary>Loads the given profile on the server.</summary>
    void LoadProfile(string name);

    /// <summary>Selects the given mode (by its <see cref="RgbMode.Index"/>) on the device, with no
    /// other change.</summary>
    void SetMode(int deviceIndex, int modeIndex);

    /// <summary>Sets every LED of the device to the given colors. <paramref name="colors"/> must
    /// have exactly as many entries as the device has LEDs.</summary>
    void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors);
}
