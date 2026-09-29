using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Network;

/// <inheritdoc cref="INetworkRemedyService"/>
public sealed class NetworkRemedyService : INetworkRemedyService
{
    private const string IpConfig = "ipconfig";

    private readonly IProcessRunner _runner;
    private readonly INetworkAdapterController _adapters;
    private readonly IElevationService _elevation;
    private readonly ILogger<NetworkRemedyService> _logger;

    public NetworkRemedyService(IProcessRunner runner, INetworkAdapterController adapters, IElevationService elevation, ILogger<NetworkRemedyService> logger)
    {
        _runner = runner;
        _adapters = adapters;
        _elevation = elevation;
        _logger = logger;
    }

    public async Task<RemedyResult> FlushDnsAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(IpConfig, ["/flushdns"], null, null, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0
            ? new RemedyResult(RemedyOutcome.Done, "Done. Your computer's saved website addresses were cleared.")
            : Failed("Windows couldn't clear the saved website addresses.");
    }

    public async Task<RemedyResult> RenewIpAsync(CancellationToken cancellationToken)
    {
        var release = await _runner.RunAsync(IpConfig, ["/release"], null, null, cancellationToken).ConfigureAwait(false);
        if (release.ExitCode != 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug)) { _logger.LogDebug("ipconfig /release exited with {ExitCode}.", release.ExitCode); }
        }

        // Once released the connection is down: always ask for a new address, even if cancelled,
        // so we never leave the PC without one.
        var renew = await _runner.RunAsync(IpConfig, ["/renew"], null, null, CancellationToken.None).ConfigureAwait(false);
        if (renew.ExitCode == 0)
        {
            return new RemedyResult(RemedyOutcome.Done, "Done. Your computer asked the router for a fresh connection.");
        }

        return _elevation.IsElevated
            ? Failed("Windows couldn't get a fresh connection from your router.")
            : new RemedyResult(
                RemedyOutcome.NeedsAdmin,
                "Windows needs administrator rights to do this. Restart Porchlight as administrator and try again.");
    }

    public async Task<RemedyResult> ResetAdapterAsync(string adapterId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(adapterId, out _))
        {
            return Failed("Porchlight couldn't tell which network connection to reset.");
        }

        if (!_elevation.IsElevated)
        {
            return new RemedyResult(
                RemedyOutcome.NeedsAdmin,
                "Resetting the network connection needs administrator rights. Restart Porchlight as administrator and try again.");
        }

        var disabled = false;
        var enabled = false;
        try
        {
            disabled = await _adapters.DisableAsync(adapterId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // Treated as "did not switch off"; Enable below still runs.
            _logger.LogDebug(ex, "Disabling the network adapter failed.");
        }
        finally
        {
            // Whatever happened to the disable (error, timeout, cancel), always switch it back on.
            enabled = await EnableAlwaysAsync(adapterId).ConfigureAwait(false);
        }

        if (disabled && enabled)
        {
            return new RemedyResult(RemedyOutcome.Done, "Done. Your network connection was switched off and on again.");
        }

        return enabled
            ? Failed("Windows wouldn't switch the network connection off. Nothing was changed.")
            : Failed("Windows couldn't switch the network connection back on. Open Windows network settings and turn it on.");
    }

    private async Task<bool> EnableAlwaysAsync(string adapterId)
    {
        try
        {
            return await _adapters.EnableAsync(adapterId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Enabling the network adapter failed.");
            return false;
        }
    }


    private static RemedyResult Failed(string message) => new(RemedyOutcome.Failed, message);
}
