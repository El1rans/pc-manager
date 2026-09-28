using Porchlight.Core.Lighting;

namespace Porchlight.App.Tests.Features.Lighting;

/// <summary>Minimal <see cref="ILightingService"/> fake for <see cref="LightingViewModelTests"/>:
/// returns fixed, configurable results - it does not model OpenRGB's actual behaviour (see
/// <c>Porchlight.Core.Tests.Lighting.LightingServiceTests</c> for that against the real service).</summary>
internal sealed class FakeLightingService : ILightingService
{
    public bool ConnectResult { get; set; }

    public bool IsConnected { get; set; }

    public IReadOnlyList<RgbDevice> Devices { get; set; } = [];

    public IReadOnlyList<string> Profiles { get; set; } = [];

    public int SetDeviceColorCallCount { get; private set; }

    public int SetAllColorCallCount { get; private set; }

    public event EventHandler? Disconnected;

    public event EventHandler? DevicesChanged;

    public void RaiseDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        IsConnected = ConnectResult;
        return Task.FromResult(ConnectResult);
    }

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Devices);

    public Task<bool> SetDeviceColorAsync(int deviceIndex, RgbColor color, CancellationToken cancellationToken)
    {
        SetDeviceColorCallCount++;
        return Task.FromResult(true);
    }

    public Task<LightingApplyResult> SetAllColorAsync(RgbColor color, CancellationToken cancellationToken)
    {
        SetAllColorCallCount++;
        return Task.FromResult(new LightingApplyResult(Devices.Count, 0));
    }

    public Task<bool> SetModeAsync(int deviceIndex, string modeName, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Profiles);

    public Task<bool> LoadProfileAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<LightingApplyResult> TurnOffAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new LightingApplyResult(Devices.Count, 0));
}
