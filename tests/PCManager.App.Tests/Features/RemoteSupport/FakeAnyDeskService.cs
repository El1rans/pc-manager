using PCManager.Core.Components;
using PCManager.Core.RemoteSupport;

namespace PCManager.App.Tests.Features.RemoteSupport;

internal sealed class FakeAnyDeskService : IAnyDeskService
{
    public AnyDeskState StateToReturn { get; set; } =
        new(IsInstalled: false, ExePath: null, Version: null, Id: null, Alias: null, IsRunning: false,
            ComponentStatus: ComponentStatus.NotInstalled);

    public AnyDeskState LaunchResult { get; set; } =
        new(IsInstalled: true, ExePath: @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe", Version: "9.7.16",
            Id: "123456789", Alias: null, IsRunning: true, ComponentStatus: new ComponentStatus(ComponentState.Running));

    public int GetStateCallCount { get; private set; }

    public int LaunchCallCount { get; private set; }

    public Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken)
    {
        GetStateCallCount++;
        return Task.FromResult(StateToReturn);
    }

    public Task<AnyDeskState> InstallAsync(IProgress<string> log, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not needed by RemoteSupportViewModel - install goes through IComponentService/ComponentCardViewModel.");

    public Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken)
    {
        LaunchCallCount++;
        StateToReturn = LaunchResult;
        return Task.FromResult(LaunchResult);
    }
}
