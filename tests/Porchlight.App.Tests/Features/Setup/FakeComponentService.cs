using Porchlight.Core.Components;

namespace Porchlight.App.Tests.Features.Setup;

internal sealed class FakeComponentService : IComponentService
{
    private readonly Dictionary<string, ComponentStatus> _statuses = new();
    private readonly Dictionary<string, Queue<ComponentStatus>> _installResults = new();

    public IReadOnlyList<ComponentDefinition> Definitions => ComponentCatalog.All;

    public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    public List<string> InstallCalls { get; } = [];

    /// <summary>How many handlers are attached to <see cref="StatusChanged"/> - lets a test assert
    /// that a disposed card really unsubscribed.</summary>
    public int StatusChangedSubscriberCount => StatusChanged?.GetInvocationList().Length ?? 0;

    public void SetStatus(string id, ComponentStatus status) => _statuses[id] = status;

    /// <summary>Queues the status <see cref="InstallAsync"/> returns for <paramref name="id"/>,
    /// one call at a time (so a retry can return a different result than the first attempt).</summary>
    public void QueueInstallResult(string id, ComponentStatus status)
    {
        if (!_installResults.TryGetValue(id, out var queue))
        {
            queue = new Queue<ComponentStatus>();
            _installResults[id] = queue;
        }

        queue.Enqueue(status);
    }

    public Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_statuses.TryGetValue(id, out var status) ? status : ComponentStatus.NotInstalled);

    public Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken)
    {
        InstallCalls.Add(id);
        var status = _installResults.TryGetValue(id, out var queue) && queue.Count > 0
            ? queue.Dequeue()
            : new ComponentStatus(ComponentState.Installed);
        _statuses[id] = status;
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(id, status));
        return Task.FromResult(status);
    }

    public Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken) =>
        GetStatusAsync(id, cancellationToken);
}
