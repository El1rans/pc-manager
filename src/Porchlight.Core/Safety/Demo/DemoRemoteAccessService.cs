namespace Porchlight.Core.Safety.Demo;

/// <summary>DEBUG demo data: Porchlight's AnyDesk plus one unknown tool.</summary>
internal sealed class DemoRemoteAccessService : IRemoteAccessService
{
    public Task<RemoteAccessStatus> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RemoteAccessStatus(
        [
            new RemoteToolFinding("anydesk", "AnyDesk", IsRunning: true, SetUpByPorchlight: true),
            new RemoteToolFinding("teamviewer", "TeamViewer", IsRunning: false, SetUpByPorchlight: false),
        ]));
}
