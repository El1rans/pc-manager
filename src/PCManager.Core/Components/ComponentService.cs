using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ComponentService> _logger;

    public ComponentService(
        IRegistryReader registryReader,
        IFileSystem fileSystem,
        IProcessProbe processProbe,
        IProcessRunner processRunner,
        ISettingsStore settingsStore,
        ILogger<ComponentService> logger)
    {
        _registryReader = registryReader;
        _fileSystem = fileSystem;
        _processProbe = processProbe;
        _processRunner = processRunner;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public IReadOnlyList<ComponentDefinition> Definitions => ComponentCatalog.All;

    public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    public Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = ComponentCatalog.Get(id);

        try
        {
            return Task.FromResult(DetectStatus(definition));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Could not detect status of component {ComponentId}.", id);
            return Task.FromResult(new ComponentStatus(ComponentState.Error, Message: "Could not check whether this is installed."));
        }
    }

    public async Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(progress);
        var definition = ComponentCatalog.Get(id);

        string[] arguments =
        [
            "install",
            "--id", definition.WingetId,
            "--exact",
            "--silent",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity",
        ];

        ComponentStatus status;
        try
        {
            var result = await _processRunner.RunAsync("winget", arguments, log, progress, cancellationToken)
                .ConfigureAwait(false);

            if (result.ExitCode == 0 || WingetExitCodes.IsAlreadyInstalled(result.ExitCode))
            {
                status = await GetStatusAsync(id, cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException)
        {
            throw;
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

        var status = await GetStatusAsync(id, cancellationToken).ConfigureAwait(false);
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

        try
        {
            _processRunner.StartDetached(status.Path, definition.StartArguments);
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

    private ComponentStatus DetectStatus(ComponentDefinition definition)
    {
        var entry = _registryReader.FindUninstallEntry(definition.UninstallDisplayNameMatch);
        var exePath = ResolveExePath(definition, entry);

        var foundByFilesOrRegistry = entry is not null || exePath is not null;
        var installed = definition.ServiceName is null
            ? foundByFilesOrRegistry
            : entry is not null && _registryReader.ServiceExists(definition.ServiceName);

        if (!installed)
        {
            return ComponentStatus.NotInstalled;
        }

        var version = entry?.DisplayVersion;

        if (definition.ProcessName is not null && _processProbe.IsRunning(definition.ProcessName))
        {
            return new ComponentStatus(ComponentState.Running, version, exePath);
        }

        return new ComponentStatus(ComponentState.Installed, version, exePath);
    }

    private string? ResolveExePath(ComponentDefinition definition, UninstallEntry? entry)
    {
        var candidates = new List<string>();

        if (definition.Id == ComponentIds.OpenRgb && _settingsStore.Current.Lighting.OpenRgbPathOverride is { Length: > 0 } overridePath)
        {
            candidates.Add(overridePath);
        }

        if (entry?.InstallLocation is { Length: > 0 } installLocation)
        {
            foreach (var relative in definition.ExeRelativePaths)
            {
                candidates.Add(Path.Combine(installLocation, Path.GetFileName(relative)));
                candidates.Add(Path.Combine(installLocation, relative));
            }
        }

        foreach (var programFilesDirectory in _fileSystem.ProgramFilesDirectories)
        {
            foreach (var relative in definition.ExeRelativePaths)
            {
                candidates.Add(Path.Combine(programFilesDirectory, relative));
            }
        }

        return candidates.FirstOrDefault(_fileSystem.FileExists);
    }

    private void RaiseStatusChanged(string id, ComponentStatus status) =>
        StatusChanged?.Invoke(this, new ComponentStatusChangeEventInfo(id, status));

    // Source-generated (guarded by IsEnabled internally) so the message is never formatted when
    // Information logging is disabled - see CA1873.
    [LoggerMessage(Level = LogLevel.Information, Message = "Installed component {ComponentId} (winget exit code 0x{ExitCode:X8}).")]
    private partial void LogInstalled(string componentId, int exitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Install of component {ComponentId} was cancelled by the user.")]
    private partial void LogInstallCancelled(string componentId);
}
