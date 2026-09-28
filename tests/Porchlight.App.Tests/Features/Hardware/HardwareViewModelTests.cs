using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Elevation;
using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.App.Tests.Features.Hardware;

public sealed class HardwareViewModelTests
{
    [Fact]
    public void Dispose_Twice_DoesNotThrowAndDisposesOwnedPawnIoCard()
    {
        var componentService = new FakeComponentService();
        var hardwareService = new FakeHardwareService();
        var settings = new FakeSettingsStore();
        using var fanControlManager = new FanControlManager(
            hardwareService, settings, new FanControlEngine(new FakeClock()), new FakeActivityMarker(),
            NullLogger<FanControlManager>.Instance);
        var viewModel = new HardwareViewModel(
            hardwareService, fanControlManager, settings, new FakeElevationService(),
            new ComponentCardViewModelFactory(componentService, NullLoggerFactory.Instance),
            NullLogger<HardwareViewModel>.Instance);
        Assert.Equal(1, componentService.StatusChangedSubscriberCount);

        viewModel.Dispose();
        var exception = Record.Exception(viewModel.Dispose);

        Assert.Null(exception);
        // The PawnIO card was created by the view model through the factory, so the view model
        // (not the container) owns disposing it - and a disposed card unsubscribes.
        Assert.Equal(0, componentService.StatusChangedSubscriberCount);
    }

    private sealed class FakeHardwareService : IHardwareService
    {
        public event EventHandler<HardwareSnapshot>? SnapshotUpdated
        {
            add { }
            remove { }
        }

        public HardwareSnapshot Latest { get; } = HardwareSnapshot.Empty(HardwareStatus.NotElevated);

        public IReadOnlyList<IFanController> Controllers { get; } = [];

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void ResetMinMax()
        {
        }

        public void RunOnOwnerThread(Action action, TimeSpan timeout, bool allowDirectFallback = true) => action();
    }

    private sealed class FakeActivityMarker : IFanControlActivityMarker
    {
        public bool Exists() => false;

        public void Create()
        {
        }

        public void Delete()
        {
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeElevationService : IElevationService
    {
        public bool IsElevated => false;

        public bool RestartElevated() => false;
    }
}
