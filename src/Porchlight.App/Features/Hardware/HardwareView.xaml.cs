using System.Windows;
using System.Windows.Controls;

namespace Porchlight.App.Features.Hardware;

public partial class HardwareView : UserControl
{
    public HardwareView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private Window? _window;

    // The view model only applies snapshot ticks while this view is actually visible (page selected,
    // window not hidden to tray) and the window is not minimized - see HardwareViewModel.SetViewActive.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.StateChanged += OnWindowStateChanged;
        }

        (DataContext as HardwareViewModel)?.SetViewActive(IsVisible);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.StateChanged -= OnWindowStateChanged;
            _window = null;
        }

        (DataContext as HardwareViewModel)?.SetViewActive(false);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        (DataContext as HardwareViewModel)?.SetViewActive(IsVisible);

    private void OnWindowStateChanged(object? sender, EventArgs e) =>
        (DataContext as HardwareViewModel)?.OnWindowStateChanged();

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
