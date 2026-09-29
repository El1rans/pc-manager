namespace Porchlight.Core.Network;

/// <summary>Runs the four-step "Fix my internet" check.</summary>
public interface INetworkTroubleshooter
{
    /// <summary>Runs every step in order, reporting each to <paramref name="progress"/>, and
    /// returns the diagnosis. Throws <see cref="OperationCanceledException"/> if cancelled.</summary>
    Task<TroubleshootReport> RunAsync(IProgress<TroubleshootStepUpdate>? progress, CancellationToken cancellationToken);
}
