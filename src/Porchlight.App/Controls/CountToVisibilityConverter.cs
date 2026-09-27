using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Porchlight.App.Controls;

/// <summary>Visible when the bound collection count is greater than zero (or the opposite, with
/// <see cref="Invert"/>) - used for a card's "nothing to show yet" placeholder text.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasItems = value is int count && count > 0;
        return hasItems != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
