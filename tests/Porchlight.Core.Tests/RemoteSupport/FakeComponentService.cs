using Porchlight.Core.Components;

namespace Porchlight.Core.Tests.RemoteSupport;

/// <summary>Minimal <see cref="IComponentService"/> fake for <c>AnyDeskService</c> tests: reports
/// whatever status was queued and records install/start calls, without touching winget or the
/// registry.</summary>
internal sealed class FakeComponentService : IComponentService
{
    private readonly Queue<ComponentStatus> _statusQueue = new();

    public IReadOnlyList<ComponentDefinition> Definitions => ComponentCatalog.All;

    public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    public ComponentStatus CurrentStatus { get; set; } = ComponentStatus.NotInstalled;

    public ComponentStatus InstallResult { get; set; } = new(ComponentState.Installed);

    public int StartCallCount { get; private set; }

    public ComponentStatus StatusAfterStart { get; set; } = new(ComponentState.Running);

    /// <summary>Queues a sequence of statuses that <see cref="GetStatusAsync"/> returns one at a
    /// time (last one repeats once the queue is empty), so a test can simulate detection changing
    /// across polls without a real background process.</summary>
    public void QueueStatuses(params ComponentStatus[] statuses)
    {
        foreach (var status in statuses)
        {
            _statusQueue.Enqueue(status);
        }
    }

    public Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_statusQueue.Count > 0 ? _statusQueue.Dequeue() : CurrentStatus);

    public Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken)
    {
        CurrentStatus = InstallResult;
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(id, InstallResult));
        return Task.FromResult(InstallResult);
    }

    public Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken)
    {
        StartCallCount++;
        CurrentStatus = StatusAfterStart;
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(id, StatusAfterStart));
        return Task.FromResult(StatusAfterStart);
    }
}
