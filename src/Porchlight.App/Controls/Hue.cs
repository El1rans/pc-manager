namespace Porchlight.App.Controls;

/// <summary>
/// A category colour from the vivid palette (Themes/Palette.Dark.xaml and Palette.Light.xaml).
/// Each value has <c>Hue{Name}Brush</c>, <c>TintBrush</c>, <c>StrokeBrush</c> and <c>WashBrush</c>
/// resources in both themes; <see cref="HueBrushes"/> wires them onto an element.
/// </summary>
public enum Hue
{
    Neutral,
    Amber,
    Blue,
    Violet,
    Green,
    Teal,
    Coral,
}
