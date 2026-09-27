using Microsoft.Extensions.Logging;
using PCManager.Core.Elevation;
using PCManager.Core.Processes;
using PCManager.Core.Settings;

namespace PCManager.Core.Components;

/// <inheritdoc cref="IComponentService"/>
public sealed partial class ComponentService : IComponentService
{
    private readonly IRegistryReader _registryReader;
    private readonly IFileSystem _fileSystem;
    private readonly IProcessProbe _processProbe;
    private readonly IProcessRunner _processRunner;
    private readonly ISettingsStore _settingsStore;
    private readonly IElevationService _elevationService;
    private readonly ILogger<ComponentService> _logger;

    public ComponentService(
        IRegistryReader registryReader,
        IFileSystem fileSystem,
        IProcessProbe processProbe,
        IProcessRunner processRunner,
        ISettingsStore settingsStore,
        IElevationService elevationService,
        ILogger<ComponentService> logger)
    {
        _registryReader = registryReader;
        _fileSystem = fileSystem;
        _processProbe = processProbe;
        _processRunner = processRunner;
        _settingsStore = settingsStore;
        _elevationService = elevationService;
        _logger = logger;
    }

    public IReadOnlyList<ComponentDefinition> Definitions => ComponentCatalog.All;

    public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    public async Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken)
    {
        var definition = ComponentCatalog.Get(id);
        var (status, _) = await DetectAsync(definition, cancellationToken).ConfigureAwait(false);
        return status;
    }

    public async Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(progress);
        var definition = ComponentCatalog.Get(id);

        // Cancellation is only honoured up to this point. Once winget actually starts, an install
        // (especially the PawnIO driver) must run to completion - killing it mid-write can leave a
        // half-installed kernel driver - so the rest of this call uses CancellationToken.None. A
        // caller that wants to back out of an in-flight install should not cancel this token; there
        // is deliberately no way to interrupt an installer once launched.
        cancellationToken.ThrowIfCancellationRequested();

        string[] arguments =
        [
            "install",
            "--id", definition.WingetId,
            "--exact",
            "--source", "winget",
            "--silent",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity",
        ];

        ComponentStatus status;
        try
        {
            var result = await _processRunner.RunAsync("winget", arguments, log, progress, CancellationToken.None)
                .ConfigureAwait(false);

            if (result.ExitCode == 0 || WingetExitCodes.IsAlreadyInstalled(result.ExitCode))
            {
                status = await GetStatusAsync(id, CancellationToken.None).ConfigureAwait(false);
                LogInstalled(id, result.ExitCode);
            }
            else if (WingetExitCodes.IsRebootRequiredToFinish(result.ExitCode))
            {
                status = await GetStatusAsync(id, CancellationToken.None).ConfigureAwait(false);
                status = status with { Message = "Restart your PC to finish setup." };
                LogInstalled(id, result.ExitCode);
            }
            else if (WingetExitCodes.IsCancelledByUser(result.ExitCode))
            {
                LogInstallCancelled(id);
                status = new ComponentStatus(ComponentState.Error, Message: "Installation was cancelled.");
            }
            else
            {
                _logger.LogWarning("Install of component {ComponentId} failed with exit code 0x{ExitCode:X8}.", id, result.ExitCode);
                status = new ComponentStatus(
                    ComponentState.Error,
                    Message: $"Installation failed (winget exit code 0x{unchecked((uint)result.ExitCode):X8}).");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogError(ex, "Could not run winget to install component {ComponentId}.", id);
            status = new ComponentStatus(ComponentState.Error, Message: "Could not start the installer. Check that winget (App Installer) is available.");
        }

        RaiseStatusChanged(id, status);
        return status;
    }

    public async Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken)
    {
        var definition = ComponentCatalog.Get(id);

        // Components with no "running" notion (e.g. the PawnIO driver) have nothing to start.
        if (definition.StartArguments is null)
        {
            return await GetStatusAsync(id, cancellationToken).ConfigureAwait(false);
        }

        var (status, pathIsTrusted) = await DetectAsync(definition, cancellationToken).ConfigureAwait(false);
        if (status.State is ComponentState.NotInstalled or ComponentState.Running)
        {
            return status;
        }

        if (status.Path is null)
        {
            var notFound = new ComponentStatus(status.State, status.Version, status.Path, $"Could not find {definition.DisplayName} to start it.");
            RaiseStatusChanged(id, notFound);
            return notFound;
        }

        if (_elevationService.IsElevated && !pathIsTrusted)
        {
            LogRefusedUntrustedStart(id, status.Path);
            var refused = new ComponentStatus(
                status.State,
                status.Version,
                status.Path,
                $"For safety, PC Manager won't start {definition.DisplayName} from this location while running as administrator.");
            RaiseStatusChanged(id, refused);
            return refused;
        }

        try
        {
            // Process.Start() does synchronous work; keep it off whatever thread called us (often
            // the UI thread, since a ComponentCard's button command awaits this directly).
            await Task.Run(() => _processRunner.StartDetached(status.Path, definition.StartArguments), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogError(ex, "Could not start component {ComponentId} at {Path}.", id, status.Path);
            var failed = new ComponentStatus(status.State, status.Version, status.Path, $"Could not start {definition.DisplayName}.");
            RaiseStatusChanged(id, failed);
            return failed;
        }

        var running = new ComponentStatus(ComponentState.Running, status.Version, status.Path);
        RaiseStatusChanged(id, running);
        return running;
    }

    /// <summary>Runs detection off the calling thread (registry enumeration and
    /// <c>Process.GetProcessesByName</c> are both synchronous I/O) and returns both the resulting
    /// status and whether its resolved exe path is one only an administrator could have placed
    /// (Program Files, or an HKLM-registered install location) - see
    /// <see cref="IComponentService.StartAsync"/>.</summary>
    private async Task<(ComponentStatus Status, bool PathIsTrusted)> DetectAsync(
        ComponentDefinition definition, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => Detect(definition), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Could not detect status of component {ComponentId}.", definition.Id);
            return (new ComponentStatus(ComponentState.Error, Message: "Could not check whether this is installed."), false);
        }
    }

    private (ComponentStatus Status, bool PathIsTrusted) Detect(ComponentDefinition definition)
    {
        var entry = _registryReader.FindUninstallEntry(definition.UninstallDisplayNameMatch);
        var resolution = ResolveExePath(definition, entry);

        var foundByFilesOrRegistry = entry is not null || resolution.Path is not null;
        var installed = definition.ServiceName is null
            ? foundByFilesOrRegistry
            : entry is not null && _registryReader.ServiceExists(definition.ServiceName);

        if (!installed)
        {
            return (ComponentStatus.NotInstalled, resolution.IsTrusted);
        }

        var version = entry?.DisplayVersion;

        if (definition.ProcessName is not null && _processProbe.IsRunning(definition.ProcessName))
        {
            return (
                new ComponentStatus(ComponentState.Running, version, resolution.Path, PathIsTrusted: resolution.IsTrusted),
                resolution.IsTrusted);
        }

        return (
            new ComponentStatus(ComponentState.Installed, version, resolution.Path, PathIsTrusted: resolution.IsTrusted),
            resolution.IsTrusted);
    }

    /// <param name="Path">The resolved executable path, or null if not found.</param>
    /// <param name="IsTrusted">
    /// True if only an administrator could have placed the file there: found under a Program
    /// Files directory, or under an HKLM (per-machine) uninstall entry's <c>InstallLocation</c>.
    /// False for a user-writable source: the OpenRGB path override in settings, or an HKCU
    /// (per-user) uninstall entry.
    /// </param>
    private sealed record ExePathResolution(string? Path, bool IsTrusted);

    private ExePathResolution ResolveExePath(ComponentDefinition definition, UninstallEntry? entry)
    {
        if (definition.Id == ComponentIds.OpenRgb &&
            _settingsStore.Current.Lighting.OpenRgbPathOverride is { Length: > 0 } overridePath &&
            _fileSystem.FileExists(overridePath))
        {
            return new ExePathResolution(overridePath, IsTrusted: false);
        }

        if (entry?.InstallLocation is { Length: > 0 } installLocation)
        {
            List<string> entryCandidates = [];
            foreach (var relative in definition.ExeRelativePaths)
            {
                entryCandidates.Add(Path.Combine(installLocation, Path.GetFileName(relative)));
                entryCandidates.Add(Path.Combine(installLocation, relative));
            }

            var entryPath = entryCandidates.FirstOrDefault(_fileSystem.FileExists);
            if (entryPath is not null)
            {
                return new ExePathResolution(entryPath, IsTrusted: entry.IsPerMachine);
            }
        }

        foreach (var programFilesDirectory in _fileSystem.ProgramFilesDirectories)
        {
            foreach (var relative in definition.ExeRelativePaths)
            {
                var candidate = Path.Combine(programFilesDirectory, relative);
                if (_fileSystem.FileExists(candidate))
                {
                    return new ExePathResolution(candidate, IsTrusted: true);
                }
            }
        }

        return new ExePathResolution(null, IsTrusted: false);
    }

    private void RaiseStatusChanged(string id, ComponentStatus status) =>
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(id, status));

    // Source-generated (guarded by IsEnabled internally) so the message is never formatted when
    // Information logging is disabled - see CA1873.
    [LoggerMessage(Level = LogLevel.Information, Message = "Installed component {ComponentId} (winget exit code 0x{ExitCode:X8}).")]
    private partial void LogInstalled(string componentId, int exitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Install of component {ComponentId} was cancelled by the user.")]
    private partial void LogInstallCancelled(string componentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused to start component {ComponentId} at {Path} while elevated: not a trusted (admin-only-writable) location.")]
    private partial void LogRefusedUntrustedStart(string componentId, string path);
}
