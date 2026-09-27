using PCManager.Core.Components;

namespace PCManager.Core.Tests.Lighting;

/// <summary>Minimal <see cref="IComponentService"/> fake for <see cref="LightingServiceTests"/>:
/// only <see cref="StatusChanged"/> is used by <see cref="LightingService"/> - it reacts to the
/// openrgb component leaving <see cref="ComponentState.Running"/> by disconnecting (see
/// docs/specs/05-lighting.md, acceptance #3). The other members are not called by that code.</summary>
public sealed class FakeComponentService : IComponentService
{
    public IReadOnlyList<ComponentDefinition> Definitions => [];

    public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    public void RaiseStatusChanged(string componentId, ComponentStatus status) =>
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(componentId, status));

    public Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(ComponentStatus.NotInstalled);

    public Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken) =>
        Task.FromResult(ComponentStatus.NotInstalled);

    public Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(ComponentStatus.NotInstalled);
}
