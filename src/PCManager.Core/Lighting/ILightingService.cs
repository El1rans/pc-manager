namespace PCManager.Core.Lighting;

/// <summary>
/// Controls RGB lighting on every device OpenRGB knows about. Talks to the OpenRGB SDK server
/// through <see cref="IOpenRgbClient"/> only - PC Manager never talks to RGB hardware directly (see
/// docs/specs/05-lighting.md).
/// </summary>
/// <remarks>
/// Every method here is safe to await from the UI thread (the work itself runs off it), times out
/// after a few seconds, and never lets an exception reach the caller: a timeout or a dropped
/// connection is logged, raises <see cref="Disconnected"/>, and the call returns a safe default
/// (false, or an empty list) instead. Callers - the Lighting page - react to
/// <see cref="Disconnected"/> by switching to a "reconnect" state.
/// </remarks>
public interface ILightingService
{
    /// <summary>Whether the last completed call reached the OpenRGB SDK server.</summary>
    bool IsConnected { get; }

    /// <summary>Raised when a call finds the connection gone (closed, timed out, or the OpenRGB
    /// process exited).</summary>
    event EventHandler? Disconnected;

    /// <summary>Connects to the OpenRGB SDK server. Returns false (and does not throw) if it could
    /// not connect, e.g. because OpenRGB isn't running.</summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Every device OpenRGB currently reports. Empty (not an exception) if not connected
    /// or the call failed.</summary>
    Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken);

    /// <summary>Sets one device to a flat color, switching it to "Direct" or "Static" mode first
    /// when available (see <see cref="LightingModeSelector"/>). Returns whether it succeeded.</summary>
    Task<bool> SetDeviceColorAsync(int deviceIndex, RgbColor color, CancellationToken cancellationToken);

    /// <summary>Sets every device to a flat color. Returns whether it succeeded.</summary>
    Task<bool> SetAllColorAsync(RgbColor color, CancellationToken cancellationToken);

    /// <summary>Switches a device to one of its own modes, by name. Returns whether it succeeded.</summary>
    Task<bool> SetModeAsync(int deviceIndex, string modeName, CancellationToken cancellationToken);

    /// <summary>Every OpenRGB profile's name. Empty (not an exception) if not connected or the call
    /// failed.</summary>
    Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken cancellationToken);

    /// <summary>Loads the given OpenRGB profile. Returns whether it succeeded.</summary>
    Task<bool> LoadProfileAsync(string name, CancellationToken cancellationToken);

    /// <summary>Sets every device to black (off). Returns whether it succeeded.</summary>
    Task<bool> TurnOffAllAsync(CancellationToken cancellationToken);
}
