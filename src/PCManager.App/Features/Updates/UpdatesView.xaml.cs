using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCManager.App.Features.Updates;

public partial class UpdatesView : UserControl
{
    public UpdatesView()
    {
        InitializeComponent();
    }

    private UpdatesViewModel? ViewModel => DataContext as UpdatesViewModel;

    /// <summary>Double-click toggles the checkbox for the row under the cursor - see
    /// <c>docs/specs/02-updates.md</c>.</summary>
    private void PackagesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PackagesGrid.SelectedItem is UpdatePackageViewModel row && !row.IsIgnored)
        {
            row.IsSelected = !row.IsSelected;
        }
    }

    private IEnumerable<UpdatePackageViewModel> SelectedRows() =>
        PackagesGrid.SelectedItems.Cast<UpdatePackageViewModel>();

    private void IgnoreMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel?.Ignore(SelectedRows());

    private void StopIgnoringMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel?.StopIgnoring(SelectedRows());

    private void CopyIdMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var ids = SelectedRows().Select(r => r.Id).ToList();
        if (ids.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, ids));
        }
    }

    private async void ShowPackageInfoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (PackagesGrid.SelectedItem is UpdatePackageViewModel row && ViewModel is { } viewModel)
        {
            await viewModel.ShowPackageInfoAsync(row, CancellationToken.None);
        }
    }

    /// <summary>Keeps the log auto-scrolled to the newest line as it grows - see
    /// <c>docs/specs/02-updates.md</c>: "monospace, auto-scroll".</summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e) => LogTextBox.ScrollToEnd();
}
