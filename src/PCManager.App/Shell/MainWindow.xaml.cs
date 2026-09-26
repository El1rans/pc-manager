using System.Windows;

namespace PCManager.App.Shell;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // First navigation happens once the window has loaded, not during DI construction.
        Loaded += async (_, _) => await viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
    }
}
