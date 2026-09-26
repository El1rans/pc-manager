using System.Windows;
using System.Windows.Threading;
using PCManager.Services;
using PCManager.Services.Winget;
using PCManager.ViewModels;

namespace PCManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeMode = ThemeMode.System;
        DispatcherUnhandledException += OnUnhandledException;

        var settings = SettingsStore.Load();
        var dashboard = new DashboardViewModel();
        var updates = new UpdatesViewModel(new WingetService(), settings);
        var main = new MainViewModel(dashboard, updates);

        MainWindow = new MainWindow { DataContext = main };
        MainWindow.Closed += (_, _) => dashboard.Dispose();
        MainWindow.Show();

        dashboard.Start();
        _ = updates.RefreshCommand.ExecuteAsync(null);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write("Unhandled error: " + e.Exception);
        MessageBox.Show(e.Exception.Message, "PC Manager - unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
