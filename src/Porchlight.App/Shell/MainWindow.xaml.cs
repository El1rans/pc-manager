using System.ComponentModel;
using System.Windows;

namespace Porchlight.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // First navigation happens once the window has loaded, not during DI construction.
        Loaded += async (_, _) => await viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
        Closing += OnClosing;
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
