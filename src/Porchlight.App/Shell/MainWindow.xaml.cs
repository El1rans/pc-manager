using System.ComponentModel;
using System.Windows;
using Porchlight.App.Tray;
using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

public partial class MainWindow : Window
{
    private readonly ISettingsStore _settingsStore;
    private readonly ITrayIcon _trayIcon;
    private readonly IShellWindowService _shellWindowService;

    public MainWindow(
        MainViewModel viewModel, ISettingsStore settingsStore, ITrayIcon trayIcon, IShellWindowService shellWindowService)
    {
        _settingsStore = settingsStore;
        _trayIcon = trayIcon;
        _shellWindowService = shellWindowService;
        InitializeComponent();
        DataContext = viewModel;
        ApplySavedBounds(settingsStore.Current.Window);

        // See WhiteFlashGuard: without this, the freshly shown window can paint solid white until
        // the user clicks it.
        WhiteFlashGuard.Attach(this);

        // First navigation happens once the window has loaded, not during DI construction.
        Loaded += async (_, _) => await viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        Closing += OnClosing;

        // MainNavList, PinnedNavList and the tab strip are independent ListBox (Selector) controls
        // that each show part of the same MainViewModel selection - but each is its own Selector with
        // its own selection state, and WPF does not reliably clear one Selector's highlighted item
        // just because a property it reads (via a OneWay SelectedItem binding - see the XAML
        // comments) changed to a value belonging to the OTHER Selector's ItemsSource. So both
        // directions here are handled explicitly instead of by binding:
        //  - user click -> view model: pushed directly (a plain property set, not a binding), from
        //    whichever list's SelectionChanged fired with a real item.
        //  - clearing the sibling list's stale highlight: SelectedIndex = -1 on the other list.
        // SelectedItem is deliberately OneWay (not TwoWay) in XAML so that clearing step never
        // round-trips back through a shared TwoWay-bound property and clobbers the selection to
        // null - which previously blanked the content area right after picking "Get help". The same
        // goes for the tab strip, whose ItemsSource is swapped whenever the category changes.
        MainNavList.SelectionChanged += (_, _) =>
        {
            if (MainNavList.SelectedItem is NavCategoryViewModel category)
            {
                PinnedNavList.SelectedIndex = -1;
                viewModel.SelectedCategory = category;
            }
        };
        PinnedNavList.SelectionChanged += (_, _) =>
        {
            if (PinnedNavList.SelectedItem is NavCategoryViewModel category)
            {
                MainNavList.SelectedIndex = -1;
                viewModel.SelectedCategory = category;
            }
        };
        TabStrip.SelectionChanged += (_, _) =>
        {
            if (TabStrip.SelectedItem is IPage page)
            {
                viewModel.SelectedPage = page;
            }
        };
    }

    /// <summary>Asks the user to confirm quitting while <paramref name="busyPage"/> has work in
    /// flight. Never stops the work itself. Returns true to go ahead.</summary>
    internal static bool ConfirmClose(IBusyGuard busyPage, Window? owner)
    {
        const string title = "Porchlight";
        var result = owner is null
            ? MessageBox.Show(busyPage.BusyMessage, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            : MessageBox.Show(owner, busyPage.BusyMessage, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// With "keep running in the tray" on (and the tray icon actually present), closing hides the
    /// window instead - nothing stops, so no busy warning. Otherwise (setting off, no icon, or the
    /// app really exiting) it asks for confirmation if any page reports work in flight via
    /// <see cref="IBusyGuard"/>; declining just cancels the close.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Every path through here - hide to tray, real exit, or a cancelled close - is a good
        // moment to remember the window's size and position for next launch.
        SaveBounds();

        if (AppExitState.IsExiting)
        {
            return;
        }

        if (_settingsStore.Current.Notifications.KeepRunningInTray && _trayIcon.IsVisible)
        {
            e.Cancel = true;
            Hide();
            ShowTrayHintOnce();
            return;
        }

        if (DataContext is MainViewModel viewModel
            && viewModel.FindBusyPage() is { } busyPage
            && !ConfirmClose(busyPage, this))
        {
            e.Cancel = true;
        }
    }

    /// <summary>Applies the saved size/position, but only if it would still be at least partly on
    /// a screen (a monitor may have been unplugged or rearranged since); otherwise the window keeps
    /// its XAML default size and Windows' default placement.</summary>
    private void ApplySavedBounds(WindowSettings saved)
    {
        if (saved is not { Left: { } left, Top: { } top, Width: { } width, Height: { } height }
            || width < MinWidth || height < MinHeight
            || !IsOnScreen(left, top, width, height))
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        if (saved.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>True when enough of the window's title bar (a 100x40 patch at its top-left) is on
    /// the virtual screen for the user to grab and move it.</summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var screen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var titleBar = new Rect(left, top, Math.Min(width, 100), Math.Min(height, 40));
        return screen.Contains(titleBar);
    }

    private void SaveBounds()
    {
        // RestoreBounds is the normal-state rectangle even while maximized or minimized, so
        // un-maximizing next session returns to the size the user last set by hand.
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left) || double.IsNaN(bounds.Top) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;
        _settingsStore.Update(s =>
        {
            s.Window.Left = bounds.Left;
            s.Window.Top = bounds.Top;
            s.Window.Width = bounds.Width;
            s.Window.Height = bounds.Height;
            s.Window.IsMaximized = isMaximized;
        });
    }

    private void ShowTrayHintOnce()
    {
        if (_settingsStore.Current.Notifications.TrayHintShown)
        {
            return;
        }

        _settingsStore.Update(s => s.Notifications.TrayHintShown = true);
        _trayIcon.ShowBalloon(
            "Porchlight is still running here",
            "Right-click this icon to open it or quit.",
            _shellWindowService.ShowMainWindow);
    }
}
