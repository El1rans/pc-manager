using Porchlight.Core.Hardware;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Fake <see cref="IHardwareService"/> for <see cref="FanControlManager"/> tests: the
/// manager only ever reads <see cref="Controllers"/> and raises/awaits through
/// <see cref="SnapshotUpdated"/>/<see cref="RunOnOwnerThread"/>, so this fake runs everything
/// synchronously, inline, on the calling thread - there is no real hardware thread to marshal onto
/// in a unit test.</summary>
public sealed class FakeHardwareService : IHardwareService
{
    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;

    public HardwareSnapshot Latest { get; private set; } = HardwareSnapshot.Empty(HardwareStatus.Ready);

    public IReadOnlyList<IFanController> Controllers { get; set; } = [];

    public int ResetMinMaxCallCount { get; private set; }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void ResetMinMax() => ResetMinMaxCallCount++;

    public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true) => action();

    public void RaiseSnapshot(HardwareSnapshot snapshot)
    {
        Latest = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
    }
}
