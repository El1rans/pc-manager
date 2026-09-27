using System.Threading;
using PCManager.Core.Lighting;

namespace PCManager.Core.Tests.Lighting;

/// <summary>Fake standing in for the OpenRGB SDK socket in <see cref="LightingServiceTests"/>,
/// so those tests exercise <see cref="LightingService"/>'s own logic (mode selection, timeouts,
/// disconnect handling) without a real OpenRGB server.</summary>
public sealed class FakeOpenRgbClient : IOpenRgbClient
{
    private readonly List<RgbDevice> _devices = [];
    private readonly List<string> _profiles = [];

    /// <summary>Signaled ("released") by default; reset by <see cref="HangUntilDisposed"/> to make
    /// every call block, and set again by <see cref="Release"/> or <see cref="Dispose"/> - mirroring
    /// OpenRGB.NET's own blocking read (<c>BlockingCollection.Take</c> with no timeout of its own),
    /// which only unblocks when the connection is disposed.</summary>
    private readonly ManualResetEventSlim _releaseSignal = new(initialState: true);

    /// <summary>When set, <see cref="Connect"/> throws this instead of connecting.</summary>
    public Exception? FailConnectionWith { get; set; }

    /// <summary>When set, every call throws this - simulates the socket dying mid-session.</summary>
    public Exception? FailAllCallsWith { get; set; }

    /// <summary>When true, every call blocks until <see cref="Dispose"/> or <see cref="Release"/> is
    /// called - used to exercise <see cref="LightingService"/>'s timeout, dispose-to-recover, and
    /// heartbeat paths against something that behaves like the real library's blocking read rather
    /// than one that conveniently throws.</summary>
    public bool HangUntilDisposed
    {
        get => !_releaseSignal.IsSet;
        set
        {
            if (value)
            {
                _releaseSignal.Reset();
            }
            else
            {
                _releaseSignal.Set();
            }
        }
    }

    public int DisposeCallCount { get; private set; }

    public int GetControllerCountCallCount { get; private set; }

    public int GetAllControllerDataCallCount { get; private set; }

    /// <summary>When a device index is present here, <see cref="UpdateLeds"/> throws its exception
    /// for that device only - used to prove a single device's own failure does not stop the rest of
    /// a "set all" loop.</summary>
    public Dictionary<int, Exception> FailUpdateLedsForDevice { get; } = [];

    public List<(int DeviceIndex, IReadOnlyList<RgbColor> Colors)> UpdateLedsCalls { get; } = [];

    public List<(int DeviceIndex, int ModeIndex, IReadOnlyList<RgbColor>? Colors)> SetModeCalls { get; } = [];

    public string? LoadedProfile { get; private set; }

    /// <summary>Public setter so a test can flip this to false independently of a call succeeding
    /// or throwing - simulating the vendored library's now-fixed phantom-reply bug, where a call
    /// could return a normal-looking result while the client already knew it was disconnected.</summary>
    public bool Connected { get; set; }

    public event EventHandler? DeviceListUpdated;

    public void AddDevice(RgbDevice device) => _devices.Add(device);

    public void AddProfile(string name) => _profiles.Add(name);

    /// <summary>Unblocks a call currently waiting on <see cref="HangUntilDisposed"/> without
    /// disposing - simulates OpenRGB answering late rather than the socket being torn down.</summary>
    public void Release() => _releaseSignal.Set();

    public void RaiseDeviceListUpdated() => DeviceListUpdated?.Invoke(this, EventArgs.Empty);

    public void Connect()
    {
        BlockUntilReleased();
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

    public int GetControllerCount()
    {
        BlockThenThrowIfFailing();
        GetControllerCountCallCount++;
        return _devices.Count;
    }

    public RgbDevice GetControllerData(int deviceIndex)
    {
        BlockThenThrowIfFailing();
        return _devices.FirstOrDefault(d => d.Index == deviceIndex)
            ?? throw new InvalidOperationException($"No fake device with index {deviceIndex}.");
    }

    public IReadOnlyList<RgbDevice> GetAllControllerData()
    {
        BlockThenThrowIfFailing();
        GetAllControllerDataCallCount++;
        return _devices;
    }

    public IReadOnlyList<string> GetProfiles()
    {
        BlockThenThrowIfFailing();
        return _profiles;
    }

    public void LoadProfile(string name)
    {
        BlockThenThrowIfFailing();
        LoadedProfile = name;
    }

    public void SetMode(int deviceIndex, int modeIndex, IReadOnlyList<RgbColor>? colors = null)
    {
        BlockThenThrowIfFailing();
        SetModeCalls.Add((deviceIndex, modeIndex, colors));
    }

    public void UpdateLeds(int deviceIndex, IReadOnlyList<RgbColor> colors)
    {
        BlockThenThrowIfFailing();

        if (FailUpdateLedsForDevice.TryGetValue(deviceIndex, out var deviceException))
        {
            throw deviceException;
        }

        // Mirrors the real OpenRGB.NET client, which throws on an empty color array - devices with
        // zero LEDs must never reach this call (LightingService is expected to skip them).
        if (colors.Count == 0)
        {
            throw new ArgumentException("The colors span is empty.", nameof(colors));
        }

        UpdateLedsCalls.Add((deviceIndex, colors.ToArray()));
    }

    public void Dispose()
    {
        DisposeCallCount++;

        // Mirrors OpenRGB.NET: disposing the connection unblocks any blocked read, but the object is
        // now unusable - further calls throw instead of quietly succeeding, so an orphaned caller
        // that was blocked on a stale call can't sneak in a reply once a new connection is made.
        _releaseSignal.Set();
    }

    private void BlockThenThrowIfFailing()
    {
        BlockUntilReleased();

        ObjectDisposedException.ThrowIf(DisposeCallCount > 0, this);

        if (FailAllCallsWith is not null)
        {
            throw FailAllCallsWith;
        }
    }

    private void BlockUntilReleased() => _releaseSignal.Wait();
}
