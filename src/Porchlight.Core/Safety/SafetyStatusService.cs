namespace Porchlight.Core.Safety;

/// <inheritdoc cref="ISafetyStatusService"/>
public sealed class SafetyStatusService(
    ISecurityStatusService security,
    IWindowsUpdateStatusService windowsUpdate,
    IRemoteAccessService remoteAccess) : ISafetyStatusService
{
    public async Task<SafetyStatus> GetAsync(CancellationToken cancellationToken)
    {
        var securityTask = security.GetAsync(cancellationToken);
        var updateTask = windowsUpdate.GetAsync(cancellationToken);
        var remoteTask = remoteAccess.GetAsync(cancellationToken);
        await Task.WhenAll(securityTask, updateTask, remoteTask).ConfigureAwait(false);
        return new SafetyStatus(securityTask.Result, updateTask.Result, remoteTask.Result);
    }
}
