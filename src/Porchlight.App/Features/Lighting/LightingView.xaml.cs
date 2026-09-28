using System.Windows;
using System.Windows.Controls;
using Porchlight.App.Controls;

namespace Porchlight.App.Features.Lighting;

public partial class LightingView : UserControl
{
    public LightingView()
    {
        InitializeComponent();
        AllDevicesColorPicker.ColorPicked += OnAllDevicesColorPicked;
    }

    /// <summary>Forwards the "All devices" <see cref="ColorWheelPicker"/>'s live drag/commit events
    /// to the view model, which decides whether/when to throttle a push to OpenRGB (see
    /// docs/specs/05-lighting.md addendum, "Color wheel picker"). The control's own
    /// <c>SelectedColorHex</c> two-way binding already keeps the bound hex in sync on every
    /// change - this is only about when to actually apply it to the devices.</summary>
    private void OnAllDevicesColorPicked(object? sender, ColorPickedEventArgs e)
    {
        if (DataContext is LightingViewModel viewModel)
        {
            viewModel.OnAllDevicesColorPicked(e.Color, e.IsFinal);
        }
    }

    /// <summary>
    /// Wires up a per-device <see cref="ColorWheelPicker"/> inside the device list's popup
    /// (<c>DataTemplate</c>-instantiated, so it cannot be named/wired in the constructor above) to
    /// its own <see cref="DeviceRowViewModel"/>'s <see cref="DeviceRowViewModel.OnColorPicked"/>.
    /// <see cref="FrameworkElement.Loaded"/> is a routed event, so it can be handled this way even
    /// from inside a <c>DataTemplate</c>.
    /// </summary>
    private void DeviceColorPicker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ColorWheelPicker picker || picker.DataContext is not DeviceRowViewModel device)
        {
            return;
        }

        // A DataTemplate'd element can be reloaded (virtualization, popup reopening) - unsubscribe
        // first so this never double-subscribes the same picker to the same device.
        picker.ColorPicked -= OnDeviceColorPicked;
        picker.ColorPicked += OnDeviceColorPicked;
    }

    private static void OnDeviceColorPicked(object? sender, ColorPickedEventArgs e)
    {
        if (sender is ColorWheelPicker { DataContext: DeviceRowViewModel device })
        {
            device.OnColorPicked(e.Color, e.IsFinal);
        }
    }
}
