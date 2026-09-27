using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PCManager.App.Features.Updates;

public partial class UpdatesView : UserControl
{
    public UpdatesView()
    {
        InitializeComponent();
    }

    private UpdatesViewModel? ViewModel => DataContext as UpdatesViewModel;

    /// <summary>Double-click toggles the checkbox for the row under the cursor - see
    /// <c>docs/specs/02-updates.md</c>. Only a double-click that actually lands on a data row
    /// counts: one on the column header, a scrollbar, or the row's own checkbox (which already
    /// toggles itself on a single click) must not also flip the selection a second time.</summary>
    private void PackagesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var element = source;
        while (element is not null and not DataGridRow)
        {
            if (element is CheckBox or DataGridColumnHeader or ScrollBar)
            {
                return;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        if (element is not DataGridRow || PackagesGrid.SelectedItem is not UpdatePackageViewModel row || row.IsIgnored)
        {
            return;
        }

        row.IsSelected = !row.IsSelected;
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
