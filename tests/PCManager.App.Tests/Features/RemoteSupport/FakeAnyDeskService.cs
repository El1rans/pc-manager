using PCManager.Core.Components;
using PCManager.Core.RemoteSupport;

namespace PCManager.App.Tests.Features.RemoteSupport;

internal sealed class FakeAnyDeskService : IAnyDeskService
{
    private readonly Queue<AnyDeskState> _stateQueue = new();

    public AnyDeskState StateToReturn { get; set; } =
        new(IsInstalled: false, ExePath: null, Version: null, Id: null, Alias: null, IsRunning: false,
            ComponentStatus: ComponentStatus.NotInstalled);

    public AnyDeskState LaunchResult { get; set; } =
        new(IsInstalled: true, ExePath: @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe", Version: "9.7.16",
            Id: "123456789", Alias: null, IsRunning: true, ComponentStatus: new ComponentStatus(ComponentState.Running));

    public int GetStateCallCount { get; private set; }

    public int LaunchCallCount { get; private set; }

    /// <summary>Queues states that <see cref="GetStateAsync"/> returns one at a time (in order)
    /// before falling back to <see cref="StateToReturn"/> once the queue is empty - lets a test
    /// simulate the address appearing on a later poll.</summary>
    public void QueueStates(params AnyDeskState[] states)
    {
        foreach (var state in states)
        {
            _stateQueue.Enqueue(state);
        }
    }

    public Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken)
    {
        GetStateCallCount++;
        var state = _stateQueue.Count > 0 ? _stateQueue.Dequeue() : StateToReturn;
        return Task.FromResult(state);
    }

    public Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken)
    {
        LaunchCallCount++;
        StateToReturn = LaunchResult;
        return Task.FromResult(LaunchResult);
    }
}
