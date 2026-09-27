using PCManager.Core.Lighting;

namespace PCManager.Core.Tests.Lighting;

/// <summary>Fake standing in for the OpenRGB SDK socket in <see cref="LightingServiceTests"/>,
/// so those tests exercise <see cref="LightingService"/>'s own logic (mode selection, timeouts,
/// disconnect handling) without a real OpenRGB server.</summary>
public sealed class FakeOpenRgbClient : IOpenRgbClient
{
    private readonly List<RgbDevice> _devices = [];
    private readonly List<string> _profiles = [];

    /// <summary>When set, <see cref="Connect"/> throws this instead of connecting.</summary>
    public Exception? FailConnectionWith { get; set; }

    /// <summary>When set, every call throws this - simulates the socket dying mid-session.</summary>
    public Exception? FailAllCallsWith { get; set; }

    /// <summary>When true, every call blocks until <see cref="Unblock"/> is called or the caller's
    /// token is cancelled - used to exercise <see cref="LightingService"/>'s timeout path.</summary>
    public bool HangCalls { get; set; }

    public int DisposeCallCount { get; private set; }

    public List<(int DeviceIndex, IReadOnlyList<RgbColor> Colors)> UpdateLedsCalls { get; } = [];

    public List<(int DeviceIndex, int ModeIndex)> SetModeCalls { get; } = [];

    public string? LoadedProfile { get; private set; }

    public bool Connected { get; private set; }

    public void AddDevice(RgbDevice device) => _devices.Add(device);

    public void AddProfile(string name) => _profiles.Add(name);

    public void Unblock() => HangCalls = false;

    public void Connect()
    {
        Block();
        if (FailAllCallsWith is not null)
        {
            throw FailAllCallsWith;
        }

        if (FailConnectionWith is not null)
        {
            Connected = false;
            throw FailConnectionWith;
        }

        Connected = true;
    }

    public IReadOnlyList<RgbDevice> GetAllControllerData()
    {
        ThrowIfFailing();
        return _devices;
    }

    public IReadOnlyList<string> GetProfiles()
    {
        ThrowIfFailing();
        return _profiles;
    }

    public void LoadProfile(string name)
    {
        ThrowIfFailing();
        LoadedProfile = name;
    }

    public void SetMode(int deviceIndex, int modeIndex)
    {
        ThrowIfFailing();
        SetModeCalls.Add((deviceIndex, modeIndex));
    }

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
    {
        ThrowIfFailing();
        UpdateLedsCalls.Add((deviceIndex, colors.ToArray()));
    }

    public void Dispose() => DisposeCallCount++;

    private void ThrowIfFailing()
    {
        Block();
        if (FailAllCallsWith is not null)
        {
            throw FailAllCallsWith;
        }
    }

    /// <summary>Busy-waits while <see cref="HangCalls"/> is set, simulating a server that never
    /// answers. <see cref="LightingService"/>'s own timeout is what ends this from the caller's
    /// side - this loop only stops if the test calls <see cref="Unblock"/> directly.</summary>
    private void Block()
    {
        var spinWait = new SpinWait();
        while (HangCalls)
        {
            spinWait.SpinOnce();
        }
    }
}
