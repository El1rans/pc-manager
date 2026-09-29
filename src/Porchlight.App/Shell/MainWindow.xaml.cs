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
