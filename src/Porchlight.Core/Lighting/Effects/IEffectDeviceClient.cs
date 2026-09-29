namespace Porchlight.Core.Lighting.Effects;

/// <summary>
/// The LED effects engine's own, narrow view of the OpenRGB SDK server - deliberately separate
/// from <c>Porchlight.Core.Lighting.IOpenRgbClient</c> (see docs/specs/11-led-effects.md's "why a
/// separate device client" note): it exposes the raw matrix map and per-LED names
/// <see cref="LedLayoutBuilder"/> needs, which the Lighting page's own DTOs strip out, and
/// <see cref="EffectEngine"/> owns its own connection through this interface so its frame loop
/// never contends with the Lighting page's own calls for <c>IOpenRgbClient</c>'s serializing
/// semaphore.
/// </summary>
/// <remarks>Not thread-safe - callers (namely <see cref="EffectEngine"/>'s own single frame-loop
/// thread) must not call this concurrently from more than one thread at a time.</remarks>
public interface IEffectDeviceClient : IDisposable
{
    /// <summary>True once <see cref="Connect"/> has succeeded and the connection is still open.</summary>
    bool Connected { get; }

    /// <summary>Connects to the OpenRGB SDK server. A no-op if already connected. Throws on
    /// failure - callers treat any exception here as "not connected".</summary>
    void Connect();

    /// <summary>Every device the server currently knows about.</summary>
    IReadOnlyList<EffectDeviceInfo> GetAllDevices();

    /// <summary>Selects the given mode (by its <see cref="EffectModeInfo.Index"/>) on the
    /// device.</summary>
    void SetMode(int deviceIndex, int modeIndex);

    /// <summary>Sets every LED of the device to the given colors. <paramref name="colors"/> must
    /// have exactly as many entries as the device has LEDs (<see cref="EffectDeviceInfo.LedCount"/>).
    /// Never call this for a device with zero LEDs. The list is only valid for the duration of the
    /// call - implementations must send/copy it synchronously and not retain it (the engine reuses
    /// the buffer for the next frame).</summary>
    void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors);
}
