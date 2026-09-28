using System.Windows;
using System.Windows.Media;

namespace Porchlight.App.Controls;

/// <summary>
/// A small area chart for the last N samples. New samples enter from the right.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Top of the scale. NaN scales to the data.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapacityProperty = DependencyProperty.Register(
        nameof(Capacity), typeof(int), typeof(Sparkline),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BaselineBrushProperty = DependencyProperty.Register(
        nameof(BaselineBrush), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80)), FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Capacity { get => (int)GetValue(CapacityProperty); set => SetValue(CapacityProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush BaselineBrush { get => (Brush)GetValue(BaselineBrushProperty); set => SetValue(BaselineBrushProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        drawingContext.DrawLine(new Pen(BaselineBrush, 1), new Point(0, h - 0.5), new Point(w, h - 0.5));

        var values = Values;
        if (values is null || values.Count < 2)
        {
            return;
        }

        var max = Maximum;
        if (double.IsNaN(max) || max <= 0)
        {
            max = values.Max() * 1.15;
            if (max <= 0)
            {
                max = 1;
            }
        }

        var step = w / Math.Max(Capacity - 1, 1);
        var x0 = w - (values.Count - 1) * step;
        const double top = 1.5;
        double Y(double v) => h - 1 - (Math.Clamp(v / max, 0, 1) * (h - 1 - top));

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var l = line.Open())
        using (var a = area.Open())
        {
            var first = new Point(x0, Y(values[0]));
            l.BeginFigure(first, false, false);
            a.BeginFigure(new Point(x0, h), true, true);
            a.LineTo(first, false, false);
            for (var i = 1; i < values.Count; i++)
            {
                var p = new Point(x0 + (i * step), Y(values[i]));
                l.LineTo(p, true, true);
                a.LineTo(p, false, false);
            }

            a.LineTo(new Point(x0 + ((values.Count - 1) * step), h), false, false);
        }

        line.Freeze();
        area.Freeze();

        var fill = Stroke.CloneCurrentValue();
        fill.Opacity = 0.18;
        drawingContext.DrawGeometry(fill, null, area);
        drawingContext.DrawGeometry(null, new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round }, line);
    }
}
