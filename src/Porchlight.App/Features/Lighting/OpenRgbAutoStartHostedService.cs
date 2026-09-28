using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Lighting;

/// <summary>
/// Implements the "Start OpenRGB with Porchlight" setting
/// (<see cref="LightingSettings.AutoStartOpenRgb"/>): if it is on and OpenRGB is installed but not
/// already running, starts it (minimized, with its SDK server) once at app startup. The toggle's
/// UI belongs to the Lighting page (milestone 05, see 05-lighting.md); this hosted service is the
/// behaviour it turns on.
/// </summary>
public sealed class OpenRgbAutoStartHostedService : IHostedService
{
    private readonly IComponentService _componentService;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<OpenRgbAutoStartHostedService> _logger;

    public OpenRgbAutoStartHostedService(
        IComponentService componentService, ISettingsStore settingsStore, ILogger<OpenRgbAutoStartHostedService> logger)
    {
        _componentService = componentService;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settingsStore.Current.Lighting.AutoStartOpenRgb)
        {
            return;
        }

        try
        {
            var status = await _componentService.GetStatusAsync(ComponentIds.OpenRgb, cancellationToken)
                .ConfigureAwait(false);

            if (status.State == ComponentState.Installed)
            {
                await _componentService.StartAsync(ComponentIds.OpenRgb, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort convenience feature: a failure here must never block or crash startup.
            _logger.LogWarning(ex, "Could not auto-start OpenRGB.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
