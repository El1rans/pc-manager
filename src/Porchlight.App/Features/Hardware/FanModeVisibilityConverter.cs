using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Porchlight.Core.Hardware;

namespace Porchlight.App.Features.Hardware;

/// <summary>Shows an element only when the bound <see cref="FanMode"/> equals the mode named by
/// <c>ConverterParameter</c> ("Fixed" or "Curve") - used to show/hide the fixed-percent slider and
/// the curve editor for a fan card's currently selected mode.</summary>
public sealed class FanModeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is FanMode mode && parameter is string target && mode.ToString() == target
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
