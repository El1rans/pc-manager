using System.Windows;
using System.Windows.Controls;

namespace Porchlight.App.Features.Network;

public partial class NetworkView : UserControl
{
    public NetworkView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Tells the view model whether the page is on screen, so the app list refreshes only
    /// while it is being looked at.</summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is NetworkViewModel viewModel)
        {
            viewModel.SetPageVisible(IsVisible);
        }
    }
}
