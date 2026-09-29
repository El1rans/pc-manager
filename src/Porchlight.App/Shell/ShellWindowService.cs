using System.Windows;

namespace Porchlight.App.Shell;

/// <inheritdoc cref="IShellWindowService"/>
public sealed class ShellWindowService(IAppLifetime appLifetime) : IShellWindowService
{
    public void ShowMainWindow() => RunOnUi(ShowCore);

    public void NavigateTo(Type pageViewModelType) => RunOnUi(() =>
    {
        ShowCore();
        if (Application.Current?.MainWindow is not MainWindow { DataContext: MainViewModel viewModel } window)
        {
            return;
        }

        var page = viewModel.Pages.Concat(viewModel.PinnedPages).FirstOrDefault(p => p.GetType() == pageViewModelType);
        if (page is not null)
        {
            window.SelectPage(page);
        }
    });

    public void RequestExit() => RunOnUi(() =>
    {
        if (Application.Current?.MainWindow is MainWindow { DataContext: MainViewModel viewModel } window
            && viewModel.FindBusyPage() is { } busy
            && !MainWindow.ConfirmClose(busy, window.IsVisible ? window : null))
        {
            return;
        }

        appLifetime.Shutdown();
    });

    private static void ShowCore()
    {
        if (Application.Current?.MainWindow is not { } window)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        // Activate alone is often refused for a background app (the taskbar just flashes); the
        // brief Topmost toggle is the standard way to genuinely bring the window forward.
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
