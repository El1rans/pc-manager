using Porchlight.Core.Components;

namespace Porchlight.Core.Safety;

/// <inheritdoc cref="IRemoteAccessService"/>
public sealed class RemoteAccessService(IRemoteToolProbe probe, IComponentService components) : IRemoteAccessService
{
    public async Task<RemoteAccessStatus> GetAsync(CancellationToken cancellationToken)
    {
        var evidence = await probe.CollectAsync(cancellationToken).ConfigureAwait(false);
        var findings = RemoteToolMatcher.Match(evidence).ToList();

        if (findings.Exists(f => f.Id == ComponentIds.AnyDesk))
        {
            // The same AnyDesk the Remote support page installs and starts.
            var status = await components.GetStatusAsync(ComponentIds.AnyDesk, cancellationToken).ConfigureAwait(false);
            if (status.State is ComponentState.Installed or ComponentState.Running)
            {
                var index = findings.FindIndex(f => f.Id == ComponentIds.AnyDesk);
                findings[index] = findings[index] with { SetUpByPorchlight = true };
            }
        }

        return new RemoteAccessStatus(findings);
    }
}
