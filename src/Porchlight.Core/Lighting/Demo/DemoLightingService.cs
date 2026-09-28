#if DEBUG
namespace Porchlight.Core.Lighting.Demo;

/// <summary>
/// DEBUG-only fake <see cref="ILightingService"/> for the "demo data" mode (see
/// <c>Monitoring.Demo.DemoDataMode</c> and CONTRIBUTING.md's "Screenshots" section) - reports a
/// fixed, made-up device list so a documentation screenshot of the Lighting page never shows the
/// real machine's actual RGB hardware. Never connects to a real OpenRGB SDK server.
/// </summary>
internal sealed class DemoLightingService : ILightingService
{
    private static readonly RgbMode[] FakeModes =
    [
        new RgbMode(0, "Direct", RgbColorMode.ModeSpecific, ColorCount: 1),
        new RgbMode(1, "Rainbow"),
        new RgbMode(2, "Off"),
    ];

    private static readonly RgbDevice[] FakeDevices =
    [
        new RgbDevice(
            0,
            "Demo Motherboard",
            RgbDeviceType.Motherboard,
            "Demo Vendor",
            FakeModes,
            "Direct",
            16,
            [new RgbZone("Main", 16)]),
        // A matrix-zone device (like the real Logitech G915) so a documentation screenshot of the
        // LED effects picker can show matrix-only effects (e.g. Pac-Man) being offered.
        new RgbDevice(
            1,
            "Demo Keyboard",
            RgbDeviceType.Keyboard,
            "Demo Vendor",
            FakeModes,
            "Direct",
            117,
            [new RgbZone("Keyboard", 117, IsMatrix: true)]),
    ];

    public bool IsConnected => true;

    public event EventHandler? Disconnected { add { } remove { } }

    public event EventHandler? DevicesChanged { add { } remove { } }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<IReadOnlyList<RgbDevice>> GetDevicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RgbDevice>>(FakeDevices);

    public Task<bool> SetDeviceColorAsync(int deviceIndex, RgbColor color, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<LightingApplyResult> SetAllColorAsync(RgbColor color, CancellationToken cancellationToken) =>
        Task.FromResult(new LightingApplyResult(FakeDevices.Length, 0));

    public Task<bool> SetModeAsync(int deviceIndex, string modeName, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["Demo profile"]);

    public Task<bool> LoadProfileAsync(string name, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<LightingApplyResult> TurnOffAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new LightingApplyResult(FakeDevices.Length, 0));
}
#endif
