using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IAppUninstaller"/>
public sealed partial class AppUninstaller : IAppUninstaller
{
    private readonly IProcessRunner _processRunner;
    private readonly IElevationService _elevationService;
    private readonly ILogger<AppUninstaller> _logger;

    public AppUninstaller(IProcessRunner processRunner, IElevationService elevationService, ILogger<AppUninstaller> logger)
    {
        _processRunner = processRunner;
        _elevationService = elevationService;
        _logger = logger;
    }

    public bool CanStartUninstall(InstalledApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return !_elevationService.IsElevated || app.IsPerMachine;
    }

    public UninstallStartResult StartUninstall(InstalledApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!CanStartUninstall(app))
        {
            LogBlockedWhileElevated(app.DisplayName);
            return UninstallStartResult.BlockedWhileElevated;
        }

        var command = UninstallCommandParser.Parse(app.UninstallString);
        if (command is null)
        {
            LogInvalidCommand(app.DisplayName);
            return UninstallStartResult.InvalidCommand;
        }

        try
        {
            _processRunner.StartDetached(command.FileName, command.Arguments);
            return UninstallStartResult.Started;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // Includes the user declining the uninstaller's own UAC prompt; not an error to shout about.
            LogStartFailed(ex, app.DisplayName);
            return UninstallStartResult.Failed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Did not launch the uninstaller for per-user entry {App} while elevated.")]
    private partial void LogBlockedWhileElevated(string app);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not parse the uninstall command for {App}.")]
    private partial void LogInvalidCommand(string app);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not start the uninstaller for {App}.")]
    private partial void LogStartFailed(Exception ex, string app);
}
