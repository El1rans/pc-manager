using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;

namespace Porchlight.Core.Tests.Lighting.Effects;

/// <summary>Fake <see cref="IEffectDeviceClient"/> for <see cref="EffectEngine"/> tests: holds an
/// in-memory device list and records every <see cref="SetMode"/>/<see cref="UpdateLeds"/> call
/// instead of talking to a real OpenRGB SDK server.</summary>
public sealed class FakeEffectDeviceClient : IEffectDeviceClient
{
    public List<EffectDeviceInfo> Devices { get; } = [];

    public List<(int DeviceIndex, int ModeIndex)> SetModeCalls { get; } = [];

    public List<(int DeviceIndex, RgbColor[] Colors)> UpdateLedsCalls { get; } = [];

    public bool Connected { get; set; }

    /// <summary>Device index that should throw from <see cref="UpdateLeds"/> - simulates one
    /// device's effect/hardware failing, for the per-device isolation tests.</summary>
    public int? FailingDeviceIndex { get; set; }

    public void Connect() => Connected = true;

    public IReadOnlyList<EffectDeviceInfo> GetAllDevices() => Devices;

    public void SetMode(int deviceIndex, int modeIndex) => SetModeCalls.Add((deviceIndex, modeIndex));

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
    {
        if (deviceIndex == FailingDeviceIndex)
        {
            throw new InvalidOperationException("Simulated device failure.");
        }

        UpdateLedsCalls.Add((deviceIndex, [.. colors]));
    }

    public void Dispose()
    {
    }
}
