using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PCManager.Core.Elevation;

namespace PCManager.App.Shell;

/// <inheritdoc cref="IShellService"/>
public sealed partial class ShellService : ObservableObject, IShellService
{
    private readonly IElevationService _elevationService;
    private readonly IAppLifetime _appLifetime;
    private readonly ILogger<ShellService> _logger;

    public ShellService(IElevationService elevationService, IAppLifetime appLifetime, ILogger<ShellService> logger)
    {
        _elevationService = elevationService;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    public bool IsElevated => _elevationService.IsElevated;

    [RelayCommand(CanExecute = nameof(CanRestartElevated))]
    private void RestartElevated()
    {
        if (_elevationService.RestartElevated())
        {
            _logger.LogInformation("Relaunched elevated; shutting down this instance.");
            _appLifetime.Shutdown();
        }

        // If it returned false, the user cancelled the UAC prompt; keep running as-is.
    }

    private bool CanRestartElevated() => !IsElevated;
}
