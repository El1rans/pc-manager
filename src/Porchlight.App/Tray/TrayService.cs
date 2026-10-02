using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.Notifications;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Features.Settings;
using Porchlight.App.Features.Updates;
using Porchlight.App.Shell;
using Porchlight.Core.Tray;

namespace Porchlight.App.Tray;

/// <summary>Owns the tray icon's menu and tooltip and maps its actions (menu items, double-click)
/// to the shell. Started once from <c>App.OnStartup</c> after the main window is shown (the icon's
/// window must be created on the UI thread, which a hosted service's <c>StartAsync</c> is not).</summary>
public sealed class TrayService : IDisposable
{
    private static readonly TimeSpan TooltipRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly ITrayIcon _trayIcon;
    private readonly IQuickStatsProvider _statsProvider;
    private readonly IShellWindowService _shell;
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger<TrayService> _logger;
    private DispatcherTimer? _timer;
    private int _refreshing;

    public TrayService(
        ITrayIcon trayIcon,
        IQuickStatsProvider statsProvider,
        IShellWindowService shell,
        IUpdateChecker updateChecker,
        ILogger<TrayService> logger)
    {
        _trayIcon = trayIcon;
        _statsProvider = statsProvider;
        _shell = shell;
        _updateChecker = updateChecker;
        _logger = logger;
    }

    /// <summary>Adds the icon and starts refreshing its tooltip. Call on the UI thread.</summary>
    public void Start()
    {
        _trayIcon.DoubleClicked += (_, _) => _shell.ShowMainWindow();
        _trayIcon.Show(
        [
            new TrayMenuItem("Open Porchlight", _shell.ShowMainWindow),
            new TrayMenuItem("Check for updates", CheckForUpdates),
            new TrayMenuItem("Get help", () => _shell.NavigateTo(typeof(RemoteSupportViewModel))),
            new TrayMenuItem("Notifications settings", () => _shell.NavigateTo(typeof(NotificationSettingsViewModel))),
            TrayMenuItem.Separator,
            new TrayMenuItem("Exit", _shell.RequestExit),
        ]);

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TooltipRefreshInterval };
        _timer.Tick += (_, _) => RefreshTooltip();
        _timer.Start();
        RefreshTooltip();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        _trayIcon.Dispose();
    }

    private void RefreshTooltip()
    {
        // Reading stats touches drives and native counters: keep it off the UI thread, and never
        // let two reads overlap.
        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                _trayIcon.SetTooltip(TrayTooltipFormatter.Format(_statsProvider.Read()));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not refresh the tray tooltip.");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshing, 0);
            }
        });
    }

    private void CheckForUpdates()
    {
        // Shows the Updates page (whose own navigation starts its first check) and then asks for a
        // fresh check; the checker skips itself if one is already running.
        _shell.NavigateTo(typeof(UpdatesViewModel));
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            await _updateChecker.CheckAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check from the tray menu failed.");
        }
    }
}
