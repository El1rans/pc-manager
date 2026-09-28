using System.Windows;
using System.Windows.Controls;

namespace Porchlight.App.Features.RemoteSupport;

public partial class RemoteSupportView : UserControl
{
    public RemoteSupportView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the small OK/Cancel "Change helper name" dialog. Handled in code-behind (not a VM
    /// command) since showing a Window is a view concern; the view model only learns the result via
    /// <see cref="RemoteSupportViewModel.SetHelperName"/>, and only if the user clicks OK - editing
    /// is a deliberate, confirmed action rather than a live-bound field a parent could misread
    /// mid-keystroke.
    /// </summary>
    private void ChangeHelperNameButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RemoteSupportViewModel viewModel)
        {
            return;
        }

        var dialog = new HelperNameDialog(viewModel.HelperName)
        {
            Owner = Window.GetWindow(this),
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.SetHelperName(dialog.ResultName);
        }
    }
}
