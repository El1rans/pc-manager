using System.Windows.Controls;

namespace Porchlight.App.Features.Hardware;

public partial class HardwareView : UserControl
{
    public HardwareView()
    {
        InitializeComponent();
    }

    /// <summary>Spec 04 addendum: re-checks for conflicting fan-control software whenever the Fans
    /// tab becomes the selected tab, so a tool started/closed after the page first loaded is still
    /// noticed without polling on every hardware snapshot tick.</summary>
    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, MainTabControl) &&
            ReferenceEquals(MainTabControl.SelectedItem, FansTabItem) &&
            DataContext is HardwareViewModel viewModel)
        {
            viewModel.OnFansTabSelected();
        }
    }
}
