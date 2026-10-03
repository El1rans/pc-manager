using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Porchlight.App.Controls;

/// <summary>
/// Gives an element (and, by inheritance, everything inside it) the brushes of one <see cref="Hue"/>.
/// Setting <c>c:HueBrushes.Hue="Blue"</c> points <see cref="BrushProperty"/>, <see cref="TintProperty"/>,
/// <see cref="StrokeProperty"/> and <see cref="WashProperty"/> at the <c>HueBlue*</c> resources as
/// dynamic references, so they follow a Light/Dark switch. Children bind to them, e.g.
/// <c>Foreground="{Binding (c:HueBrushes.Brush), RelativeSource={RelativeSource Self}}"</c>.
/// </summary>
public static class HueBrushes
{
    /// <summary>Default of <see cref="HueProperty"/>, so explicitly setting <see cref="Hue.Neutral"/> still
    /// raises a change and wires the neutral brushes.</summary>
    private const Hue Unset = (Hue)(-1);

    public static readonly DependencyProperty HueProperty = DependencyProperty.RegisterAttached(
        "Hue", typeof(Hue), typeof(HueBrushes), new FrameworkPropertyMetadata(Unset, OnHueChanged));

    public static readonly DependencyProperty BrushProperty = RegisterBrush("Brush");

    public static readonly DependencyProperty TintProperty = RegisterBrush("Tint");

    public static readonly DependencyProperty StrokeProperty = RegisterBrush("Stroke");

    public static readonly DependencyProperty WashProperty = RegisterBrush("Wash");

    /// <summary>On a <see cref="Border"/>: paint its border with the hue's stroke brush. Set from code
    /// (a dynamic resource reference) rather than a style binding to <see cref="StrokeProperty"/>,
    /// which WPF failed to resolve on the card the hue is set on.</summary>
    public static readonly DependencyProperty PaintBorderProperty = DependencyProperty.RegisterAttached(
        "PaintBorder", typeof(bool), typeof(HueBrushes), new FrameworkPropertyMetadata(false, OnPaintChanged));

    /// <summary>On a <see cref="Border"/>: paint its background with the hue's wash brush.</summary>
    public static readonly DependencyProperty PaintWashProperty = DependencyProperty.RegisterAttached(
        "PaintWash", typeof(bool), typeof(HueBrushes), new FrameworkPropertyMetadata(false, OnPaintChanged));

    public static Hue GetHue(DependencyObject element) => (Hue)element.GetValue(HueProperty);

    public static void SetHue(DependencyObject element, Hue value) => element.SetValue(HueProperty, value);

    public static bool GetPaintBorder(DependencyObject element) => (bool)element.GetValue(PaintBorderProperty);

    public static void SetPaintBorder(DependencyObject element, bool value) => element.SetValue(PaintBorderProperty, value);

    public static bool GetPaintWash(DependencyObject element) => (bool)element.GetValue(PaintWashProperty);

    public static void SetPaintWash(DependencyObject element, bool value) => element.SetValue(PaintWashProperty, value);

    public static Brush? GetBrush(DependencyObject element) => (Brush?)element.GetValue(BrushProperty);

    public static void SetBrush(DependencyObject element, Brush? value) => element.SetValue(BrushProperty, value);

    public static Brush? GetTint(DependencyObject element) => (Brush?)element.GetValue(TintProperty);

    public static void SetTint(DependencyObject element, Brush? value) => element.SetValue(TintProperty, value);

    public static Brush? GetStroke(DependencyObject element) => (Brush?)element.GetValue(StrokeProperty);

    public static void SetStroke(DependencyObject element, Brush? value) => element.SetValue(StrokeProperty, value);

    public static Brush? GetWash(DependencyObject element) => (Brush?)element.GetValue(WashProperty);

    public static void SetWash(DependencyObject element, Brush? value) => element.SetValue(WashProperty, value);

    /// <summary>The resource key of one of a hue's brushes, e.g. ("Blue", "Tint") -> "HueBlueTintBrush".</summary>
    internal static string ResourceKey(Hue hue, string part) => part == "Brush" ? $"Hue{hue}Brush" : $"Hue{hue}{part}Brush";

    private static DependencyProperty RegisterBrush(string part) => DependencyProperty.RegisterAttached(
        part, typeof(Brush), typeof(HueBrushes),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        var hue = (Hue)e.NewValue;
        if (!Enum.IsDefined(hue))
        {
            return;
        }

        element.SetResourceReference(BrushProperty, ResourceKey(hue, "Brush"));
        element.SetResourceReference(TintProperty, ResourceKey(hue, "Tint"));
        element.SetResourceReference(StrokeProperty, ResourceKey(hue, "Stroke"));
        element.SetResourceReference(WashProperty, ResourceKey(hue, "Wash"));
        Paint(element, hue);
    }

    private static void OnPaintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && Enum.IsDefined(GetHue(element)))
        {
            Paint(element, GetHue(element));
        }
    }

    private static void Paint(FrameworkElement element, Hue hue)
    {
        if (element is not Border border)
        {
            return;
        }

        if (GetPaintBorder(border))
        {
            border.SetResourceReference(Border.BorderBrushProperty, ResourceKey(hue, "Stroke"));
        }

        if (GetPaintWash(border))
        {
            border.SetResourceReference(Border.BackgroundProperty, ResourceKey(hue, "Wash"));
        }
    }
}
