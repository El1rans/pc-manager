using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Porchlight.Core.Lighting;

namespace Porchlight.App.Controls;

/// <summary>Converts a <c>#RRGGBB</c> string (as used throughout the Lighting page - swatches,
/// favorites, the hex box) to a frozen <see cref="SolidColorBrush"/> for a swatch button's
/// background. Invalid input converts to transparent rather than throwing, since it can be bound
/// to a textbox mid-edit.</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && RgbColor.TryParse(hex, out var color))
        {
            var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
