using Porchlight.Core.Lighting.Effects;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class FakeDeviceExclusionProvider : IDeviceExclusionProvider
{
    public HashSet<string> ExcludedDeviceNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsExcluded(string deviceName) => ExcludedDeviceNames.Contains(deviceName);
}

public sealed class FakePendingUpdateCountProvider : IPendingUpdateCountProvider
{
    public int Count { get; set; }

    public int GetPendingUpdateCount() => Count;
}

public sealed class FakeKeyPressSource : IKeyPressSource
{
    public event EventHandler<KeyPressEvent>? KeyPressed;

    public void Raise(KeyPressEvent e) => KeyPressed?.Invoke(this, e);
}
