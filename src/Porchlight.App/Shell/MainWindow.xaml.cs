using System.ComponentModel;
using System.Windows;

namespace Porchlight.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // See WhiteFlashGuard: without this, the freshly shown window can paint solid white until
        // the user clicks it.
        WhiteFlashGuard.Attach(this);

        // First navigation happens once the window has loaded, not during DI construction.
        Loaded += async (_, _) => await viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        Closing += OnClosing;

        // MainNavList and PinnedNavList are two independent ListBox (Selector) controls, both
        // showing the same MainViewModel.SelectedPage - but each is its own Selector with its own
        // selection state, and WPF does not reliably clear one Selector's highlighted ListBoxItem
        // just because a property it reads (via a OneWay SelectedItem binding - see the XAML
        // comments) changed to a value belonging to the OTHER Selector's ItemsSource. So both
        // directions here are handled explicitly instead of by binding:
        //  - user click -> SelectedPage: pushed to the view model directly (a plain property
        //    set, not a binding), from whichever list's SelectionChanged fired with a real item.
        //  - clearing the sibling list's stale highlight: SelectedIndex = -1 on the other list.
        // SelectedItem is deliberately OneWay (not TwoWay) in XAML so that clearing step never
        // round-trips back through a shared TwoWay-bound property and clobbers SelectedPage to
        // null - which previously blanked the content area right after picking "Get help" (the
        // pinned list's own clear-the-other-list step ended up wiping the very selection it had
        // just set, because both lists' SelectedItem were TwoWay-bound to the same property).
        MainNavList.SelectionChanged += (_, _) =>
        {
            if (MainNavList.SelectedItem is IPage page)
            {
                PinnedNavList.SelectedIndex = -1;
                viewModel.SelectedPage = page;
            }
        };
        PinnedNavList.SelectionChanged += (_, _) =>
        {
            if (PinnedNavList.SelectedItem is IPage page)
            {
                MainNavList.SelectedIndex = -1;
                viewModel.SelectedPage = page;
            }
        };
    }

    /// <summary>
    /// Asks for confirmation before closing if any page reports work in flight via
    /// <see cref="IBusyGuard"/> (e.g. the Updates page mid-upgrade) - see that interface's remarks.
    /// Never stops the work itself; declining just cancels the close.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        IBusyGuard? busyPage = null;
        foreach (var page in viewModel.Pages)
        {
            if (page is IBusyGuard { IsBusyWithWork: true } guard)
            {
                busyPage = guard;
                break;
            }
        }

        if (busyPage is null)
        {
            return;
        }

        var result = MessageBox.Show(
            busyPage.BusyMessage,
            "Porchlight",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }
}
