using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.App.Controls;
using Porchlight.App.Features.Dashboard;
using Porchlight.App.Features.Hardware;
using Porchlight.App.Features.Lighting;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Features.Setup;
using Porchlight.App.Features.Updates;
using Porchlight.App.Shell;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;
using Porchlight.Core.Hardware;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Settings;
using Serilog;
using Serilog.Extensions.Logging;

namespace Porchlight.App;

public partial class App : System.Windows.Application, IDisposable
{
    /// <summary>How long shutdown waits for the host to stop and dispose before giving up.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Fixed, well-known name for the app's single-instance mutex. The installer's
    /// <c>[Setup]</c> section references this same name via <c>AppMutex</c>
    /// (<c>installer/Porchlight.iss</c>) so Inno Setup can detect a running Porchlight and ask the
    /// user to close it before install/uninstall proceeds - see docs/specs/07-installer.md. Held
    /// for the lifetime of the process only to make the app detectable; it does not enforce
    /// single-instance behaviour on its own.
    /// </summary>
    public const string AppMutexName = "PorchlightAppMutex";

    /// <summary>
    /// Same mutex as <see cref="AppMutexName"/>, but in the "Global\" kernel object namespace
    /// (visible across Terminal Services sessions), not the implicit per-session "Local\"
    /// namespace <see cref="AppMutexName"/> lives in. Porchlight can autostart in any signed-in
    /// user's session via the installer's Common Startup shortcut, so Setup - itself running in
    /// whichever session launched it, not necessarily the same one - needs a name it can see from
    /// any session to detect an instance running elsewhere. A standard (non-elevated) user can
    /// create a "Global\" mutex; no special privilege is required.
    /// </summary>
    public const string GlobalAppMutexName = "Global\\PorchlightAppMutex";

    private IHost? _host;
    private Mutex? _appMutex;
    private Mutex? _globalAppMutex;

    /// <summary>
    /// Lets controls created outside DI (e.g. a <see cref="System.Windows.FrameworkElement"/>
    /// instantiated by WPF itself, not resolved from the container) reach shared services, such as
    /// <see cref="Controls.AdminRequiredBanner"/> resolving its default restart command.
    /// </summary>
    public static IServiceProvider? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeMode = ThemeMode.System;

        var logPath = BuildLogPath();

        // A bootstrap logger so a failure before (or during) host construction is still on record,
        // even though the "real" logger (wired into DI below) does not exist yet.
        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateBootstrapLogger();

        // Must run before anything reads settings.json or the fan-control marker from the new
        // %APPDATA%\Porchlight location (including a hosted service constructed during
        // _host.StartAsync() below) - see AppDataMigrator. Uses the bootstrap Serilog logger,
        // wrapped as an ILogger, since the DI container (and its "real" logger) does not exist yet.
        // WARNING: nothing above this point may log anything through the bootstrap logger. Its
        // file sink creates %APPDATA%\Porchlight\logs itself on the first write - which would make
        // AppDataMigrator.NewDirectory already exist and silently skip the migration below.
        using (var migrationLoggerFactory = new SerilogLoggerFactory(Serilog.Log.Logger, dispose: false))
        {
            AppDataMigrator.MigrateIfNeeded(migrationLoggerFactory.CreateLogger(nameof(AppDataMigrator)));
        }

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Named mutexes the installer/uninstaller looks for (Inno Setup's AppMutex, given both
        // names comma-separated) so it can ask the user to close Porchlight before install/
        // uninstall touches its files. Not used here to enforce single-instance behaviour - just
        // held for the process lifetime so they exist while the app is running. Both are created
        // regardless of which session/user is running: the "Global\" one is what lets Setup find
        // an instance autostarted in a different session (see GlobalAppMutexName).
        _appMutex = new Mutex(initiallyOwned: false, name: AppMutexName);
        _globalAppMutex = new Mutex(initiallyOwned: false, name: GlobalAppMutexName);

        try
        {
            var builder = Host.CreateApplicationBuilder(e.Args);
            ConfigureLogging(builder, logPath);
            ConfigureServices(builder.Services);

            _host = builder.Build();
            Services = _host.Services;

            // Run off the UI thread: StartAsync should not block the dispatcher, and offloading it
            // to the thread pool avoids any risk of deadlocking on this thread's context.
            Task.Run(() => _host.StartAsync()).GetAwaiter().GetResult();

            ApplyPageRegistrations(_host.Services);

            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Porchlight starting up.");

            var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
            settingsStore.Update(s => s.Setup.LaunchCount++);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            if (!settingsStore.Current.Setup.FirstRunCompleted)
            {
                _host.Services.GetRequiredService<ISetupLauncher>().ShowSetup();
            }
        }
        catch (Exception ex)
        {
            // Top-level startup guard: nothing above this point has a UI yet, so a failure here
            // would otherwise leave an invisible, unkillable-from-the-taskbar zombie process with
            // no explanation. Log it, tell the user, and exit cleanly instead.
            Serilog.Log.Fatal(ex, "Porchlight failed to start.");
            MessageBox.Show(
                "Porchlight could not start. Details were saved to the log.",
                "Porchlight - startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(ShutdownTimeout);

                // Run off the UI thread and bound it with a timeout: a hung IHostedService or a
                // singleton whose DisposeAsync never completes must not hang app shutdown forever.
                Task.Run(async () =>
                {
                    await _host.StopAsync(cts.Token).ConfigureAwait(false);
                    await ((IAsyncDisposable)_host).DisposeAsync().ConfigureAwait(false);
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Best-effort shutdown: the process is exiting either way, but the failure should
                // still be on record rather than silently swallowed.
                Serilog.Log.Error(ex, "Error while shutting down the host.");
            }
        }

        Serilog.Log.CloseAndFlush();
        Dispose();
        base.OnExit(e);
    }

    /// <summary>Disposes <see cref="_appMutex"/> and <see cref="_globalAppMutex"/>. Called from
    /// <see cref="OnExit"/> - satisfies CA1001 (a type that owns a disposable field must itself be
    /// disposable) rather than being invoked by the WPF framework itself.</summary>
    public void Dispose()
    {
        _appMutex?.Dispose();
        _globalAppMutex?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string BuildLogPath()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Porchlight",
            "logs");
        return Path.Combine(logDirectory, "porchlight-.log");
    }

    private static void ConfigureLogging(HostApplicationBuilder builder, string logPath)
    {
        builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14));
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<IAppLifetime, AppLifetime>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IPageViewLocator, PageViewLocator>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddComponents();
        services.AddSingleton<IComponentCardViewModelFactory, ComponentCardViewModelFactory>();
        services.AddSetupFeature();

        services.AddDashboardFeature();
        services.AddUpdatesFeature();
        services.AddHardwareFeature();
        services.AddLightingFeature();
        services.AddLedEffectsCore();
        services.AddHostedService<Features.Lighting.OpenRgbAutoStartHostedService>();
        services.AddRemoteSupportFeature();
    }

    /// <summary>Applies every feature's <see cref="PageRegistration"/> to the view locator. Runs
    /// once, after the container is built; adding a feature never requires touching this method.</summary>
    private static void ApplyPageRegistrations(IServiceProvider services)
    {
        var locator = services.GetRequiredService<IPageViewLocator>();
        foreach (var registration in services.GetServices<PageRegistration>())
        {
            locator.RegisterFactory(registration.ViewModelType, registration.CreateView);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        SuspendFansBestEffort();

        // If the crash happened during a window's own first layout/render pass, that window could
        // still be cloaked by WhiteFlashGuard - its own fallback timer would eventually reveal it,
        // but not necessarily before the dialog below tries to show owned by it. Force every
        // cloaked window visible now so the dialog is never hidden behind one.
        WhiteFlashGuard.UncloakAll();

        LogUnhandledException(e.Exception, "Unhandled dispatcher exception.");
        ShowUnexpectedErrorDialog();
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        SuspendFansBestEffort();
        if (e.ExceptionObject is Exception ex)
        {
            LogUnhandledException(ex, "Unhandled AppDomain exception.");
        }
    }

    /// <summary>Fan-control rule 5's crash-handler leg: a crash must never leave a fan under
    /// software control. Uses <c>Suspend</c> rather than a plain restore (B2) - unlike a system
    /// suspend, nothing here ever calls <c>ResumeFromSuspend</c>, so control stays paused (the user
    /// must re-arm from the Hardware page) rather than the very next tick silently re-applying
    /// whatever was last commanded. Best-effort - the process may be in a bad state, so this must
    /// not throw.</summary>
    private void SuspendFansBestEffort()
    {
        try
        {
            // N5: a crash is not a resumable system suspend - stays paused until the user
            // explicitly re-arms from the Hardware page.
            _host?.Services.GetService<FanControlManager>()?.Suspend("unhandled exception", resumableBySystemResume: false);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Could not pause/restore fans to default control while handling a crash.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }

    private void ShowUnexpectedErrorDialog()
    {
        // Fixed, friendly text only: the exception itself (which may include file paths or other
        // details not meant for the user) goes to the log, never into this dialog.
        const string message = "Something went wrong. Details were saved to the log. You can keep working or restart Porchlight.";
        const string title = "Porchlight - unexpected error";

        if (MainWindow is { IsLoaded: true } owner)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LogUnhandledException(Exception exception, string message)
    {
        var logger = _host?.Services.GetService<ILogger<App>>();
        if (logger is not null)
        {
            logger.LogError(exception, "{Message}", message);
        }
        else
        {
            Serilog.Log.Error(exception, "{Message}", message);
        }
    }
}
