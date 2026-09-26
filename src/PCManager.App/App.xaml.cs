using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCManager.App.Features.Dashboard;
using PCManager.App.Features.Hardware;
using PCManager.App.Features.Lighting;
using PCManager.App.Features.Updates;
using PCManager.App.Shell;
using PCManager.Core.Elevation;
using PCManager.Core.Settings;
using Serilog;

namespace PCManager.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeMode = ThemeMode.System;

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var builder = Host.CreateApplicationBuilder(e.Args);
        ConfigureLogging(builder);
        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();
        _host.Services.GetRequiredService<ILogger<App>>().LogInformation("PC Manager starting up.");

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        Serilog.Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PCManager",
            "logs");
        var logPath = Path.Combine(logDirectory, "pcmanager-.log");

        builder.Services.AddSerilog((_, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14));
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddDashboardFeature();
        services.AddUpdatesFeature();
        services.AddHardwareFeature();
        services.AddLightingFeature();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception, "Unhandled dispatcher exception.");
        MessageBox.Show(
            e.Exception.Message,
            "PC Manager - unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogUnhandledException(ex, "Unhandled AppDomain exception.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnhandledException(e.Exception, "Unobserved task exception.");
        e.SetObserved();
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
