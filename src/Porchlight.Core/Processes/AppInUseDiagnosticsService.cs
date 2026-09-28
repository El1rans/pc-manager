using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;

namespace Porchlight.Core.Processes;

/// <inheritdoc cref="IAppInUseDiagnosticsService"/>
public sealed class AppInUseDiagnosticsService : IAppInUseDiagnosticsService
{
    private readonly IRegistryReader _registryReader;
    private readonly IAppLockDetector _lockDetector;
    private readonly ILogger<AppInUseDiagnosticsService> _logger;

    public AppInUseDiagnosticsService(
        IRegistryReader registryReader, IAppLockDetector lockDetector, ILogger<AppInUseDiagnosticsService> logger)
    {
        _registryReader = registryReader;
        _lockDetector = lockDetector;
        _logger = logger;
    }

    public Task<string?> TryDescribeLockingProcessesAsync(string packageId, string displayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);
        ArgumentException.ThrowIfNullOrEmpty(displayName);

        // The registry lookup and the Restart Manager query are both blocking - run them off the
        // caller's thread here (once), rather than making every caller remember to do it - see
        // this interface's remarks.
        return Task.Run(() => TryDescribeLockingProcessesCore(packageId, displayName));
    }

    private string? TryDescribeLockingProcessesCore(string packageId, string displayName)
    {
        try
        {
            var installLocation = _registryReader.FindUninstallEntry(displayName)?.InstallLocation;
            if (string.IsNullOrWhiteSpace(installLocation))
            {
                return null;
            }

            var names = _lockDetector.FindLockingProcessNames(installLocation);
            return names.Count == 0 ? null : AppInUseExplanation.Build(displayName, names);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best-effort enrichment only - the caller falls back to the generic "close the app"
            // explanation (see AppInUseExplanation.GenericExplanation) when this returns null, so a
            // failure here must never break the update flow itself.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    ex, "Could not determine which programs are locking {PackageId}'s files.", packageId);
            }

            return null;
        }
    }
}
