using System.Globalization;
using System.Windows.Data;

namespace PCManager.App.Controls;

/// <summary>Negates a bool, e.g. binding <c>IsEnabled</c> to "not already installed".</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
