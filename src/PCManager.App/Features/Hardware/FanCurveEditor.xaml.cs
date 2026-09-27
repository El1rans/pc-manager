using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCManager.App.Features.Hardware;

/// <summary>
/// A small chart with draggable points for editing a <see cref="Core.Hardware.FanCurve"/> (spec 04:
/// "curve editor (a small chart with draggable points; points snap to 1 C and 1%)"). Temperature
/// runs 0-100 C left to right; percent runs 0-100% bottom to top. Dragging a point clamps it
/// between its neighbours (temperatures must stay strictly increasing, percentages non-decreasing)
/// and to <see cref="MinPercent"/>-100, so the curve is always valid while editing.
/// </summary>
public partial class FanCurveEditor : UserControl
{
    private const double TemperatureAxisMax = 100;
    private const double PointRadius = 6;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(System.Collections.ObjectModel.ObservableCollection<EditableCurvePoint>), typeof(FanCurveEditor),
        new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty MinPercentProperty = DependencyProperty.Register(
        nameof(MinPercent), typeof(double), typeof(FanCurveEditor),
        new PropertyMetadata(30.0, (d, _) => ((FanCurveEditor)d).Redraw()));

    public static readonly DependencyProperty CommitCommandProperty = DependencyProperty.Register(
        nameof(CommitCommand), typeof(ICommand), typeof(FanCurveEditor));

    private readonly Dictionary<EditableCurvePoint, Ellipse> _pointElements = [];
    private Polyline? _polyline;
    private EditableCurvePoint? _dragging;

    public FanCurveEditor()
    {
        InitializeComponent();

        // S9: capture on the canvas itself, not the individual point being dragged. A capture held
        // by a child element (the ellipse) is lost the instant that element is removed from the
        // visual tree - which a naive "clear and rebuild everything" Redraw() does on every mouse
        // move. Capturing on the canvas (which is never rebuilt) survives that, and dragging is
        // handled here by repositioning only the dragged point instead of a full Redraw().
        PART_Canvas.MouseMove += OnCanvasMouseMove;
        PART_Canvas.MouseLeftButtonUp += OnCanvasMouseUp;

        // S9 (recommended follow-up): capture can be lost without a MouseUp ever firing - alt-Tab,
        // a modal dialog stealing focus, or the mouse leaving the window while a button is still
        // down. Without handling this, _dragging would stay set forever, silently ignoring every
        // future click until the app restarts. Commit whatever the point's current position already
        // is (it was live-updated during the drag regardless) and clear drag state cleanly.
        PART_Canvas.LostMouseCapture += OnCanvasLostMouseCapture;
    }

    public System.Collections.ObjectModel.ObservableCollection<EditableCurvePoint>? Points
    {
        get => (System.Collections.ObjectModel.ObservableCollection<EditableCurvePoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double MinPercent
    {
        get => (double)GetValue(MinPercentProperty);
        set => SetValue(MinPercentProperty, value);
    }

    public ICommand? CommitCommand
    {
        get => (ICommand?)GetValue(CommitCommandProperty);
        set => SetValue(CommitCommandProperty, value);
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (FanCurveEditor)d;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= editor.OnPointsCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += editor.OnPointsCollectionChanged;
        }

        editor.Redraw();
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Redraw();

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    /// <summary>Full rebuild - only for structural changes (points added/removed, resize, initial
    /// bind). Never called while a drag is in progress; see <see cref="OnCanvasMouseMove"/>.</summary>
    private void Redraw()
    {
        PART_Canvas.Children.Clear();
        _pointElements.Clear();
        _polyline = null;

        var points = Points;
        var width = PART_Canvas.ActualWidth;
        var height = PART_Canvas.ActualHeight;
        if (points is null || points.Count == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var ordered = points.OrderBy(p => p.TemperatureC).ToList();

        var polyline = new Polyline
        {
            Stroke = (Brush)FindResource("AccentFillColorDefaultBrush"),
            StrokeThickness = 2,
        };
        foreach (var point in ordered)
        {
            polyline.Points.Add(ToCanvas(point, width, height));
        }

        PART_Canvas.Children.Add(polyline);
        _polyline = polyline;

        foreach (var point in ordered)
        {
            var canvasPoint = ToCanvas(point, width, height);
            var ellipse = new Ellipse
            {
                Width = PointRadius * 2,
                Height = PointRadius * 2,
                Fill = (Brush)FindResource("AccentFillColorDefaultBrush"),
                Cursor = Cursors.SizeAll,
                Tag = point,
            };
            Canvas.SetLeft(ellipse, canvasPoint.X - PointRadius);
            Canvas.SetTop(ellipse, canvasPoint.Y - PointRadius);
            ellipse.MouseLeftButtonDown += OnPointMouseDown;
            PART_Canvas.Children.Add(ellipse);
            _pointElements[point] = ellipse;
        }
    }

    private static Point ToCanvas(EditableCurvePoint point, double width, double height)
    {
        var x = Math.Clamp(point.TemperatureC / TemperatureAxisMax, 0, 1) * width;
        var y = (1 - Math.Clamp(point.Percent / 100, 0, 1)) * height;
        return new Point(x, y);
    }

    private void OnPointMouseDown(object sender, MouseButtonEventArgs e)
    {
        var ellipse = (Ellipse)sender;
        _dragging = (EditableCurvePoint)ellipse.Tag;
        PART_Canvas.CaptureMouse();
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null || e.LeftButton != MouseButtonState.Pressed || Points is null)
        {
            return;
        }

        var width = PART_Canvas.ActualWidth;
        var height = PART_Canvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var position = e.GetPosition(PART_Canvas);
        var temperature = Math.Round(Math.Clamp(position.X / width, 0, 1) * TemperatureAxisMax);
        var percent = Math.Round((1 - Math.Clamp(position.Y / height, 0, 1)) * 100);

        var ordered = Points.OrderBy(p => p.TemperatureC).ToList();
        var index = ordered.IndexOf(_dragging);
        if (index < 0)
        {
            return;
        }

        var minTemp = index > 0 ? ordered[index - 1].TemperatureC + 1 : 0;
        var maxTemp = index < ordered.Count - 1 ? ordered[index + 1].TemperatureC - 1 : TemperatureAxisMax;
        var minPct = Math.Max(MinPercent, index > 0 ? ordered[index - 1].Percent : MinPercent);
        var maxPct = index < ordered.Count - 1 ? ordered[index + 1].Percent : 100;

        _dragging.TemperatureC = Math.Clamp(temperature, minTemp, Math.Max(minTemp, maxTemp));
        _dragging.Percent = Math.Clamp(percent, minPct, Math.Max(minPct, maxPct));

        RepositionDraggedPoint(width, height);
    }

    /// <summary>Moves only the dragged point's ellipse and refreshes the polyline - unlike
    /// <see cref="Redraw"/>, this never touches the visual tree's element identity, so the mouse
    /// capture taken in <see cref="OnPointMouseDown"/> (on the canvas) is never disturbed.</summary>
    private void RepositionDraggedPoint(double width, double height)
    {
        if (_dragging is null || Points is null)
        {
            return;
        }

        if (_pointElements.TryGetValue(_dragging, out var ellipse))
        {
            var canvasPoint = ToCanvas(_dragging, width, height);
            Canvas.SetLeft(ellipse, canvasPoint.X - PointRadius);
            Canvas.SetTop(ellipse, canvasPoint.Y - PointRadius);
        }

        if (_polyline is not null)
        {
            var ordered = Points.OrderBy(p => p.TemperatureC).ToList();
            _polyline.Points = new PointCollection(ordered.Select(p => ToCanvas(p, width, height)));
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging is null)
        {
            return;
        }

        // ReleaseMouseCapture() below raises LostMouseCapture synchronously, which does the actual
        // "clear _dragging and commit" work - see OnCanvasLostMouseCapture - so both the normal
        // mouse-up path and an abnormal capture loss end up in exactly one place.
        PART_Canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnCanvasLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragging is null)
        {
            return;
        }

        _dragging = null;

        if (CommitCommand?.CanExecute(null) == true)
        {
            CommitCommand.Execute(null);
        }
    }
}
