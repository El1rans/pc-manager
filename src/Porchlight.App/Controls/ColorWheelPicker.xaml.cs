using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Porchlight.Core.Lighting;

namespace Porchlight.App.Controls;

/// <summary>
/// Reusable HSV color picker: a hue/saturation wheel (drag or arrow keys) plus a value
/// (brightness-of-the-hue) slider, a live preview swatch, and a hex box kept in sync both ways.
/// Bind <see cref="SelectedColorHex"/> like any other control (it is two-way by default); handle
/// <see cref="ColorPicked"/> to apply the color live while dragging - throttle non-final updates
/// (see <see cref="Porchlight.Core.Lighting.ColorApplyRateLimiter"/>) but always apply a final one.
/// See docs/specs/05-lighting.md addendum, "Color wheel picker".
/// </summary>
public partial class ColorWheelPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorHexProperty = DependencyProperty.Register(
        nameof(SelectedColorHex), typeof(string), typeof(ColorWheelPicker),
        new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexPropertyChanged));

    /// <summary>Arrow-key hue step in degrees - about 1/72 of the wheel per press, matching a
    /// typical color picker's keyboard granularity.</summary>
    private const double HueKeyStepDegrees = 5;

    /// <summary>Arrow-key saturation step.</summary>
    private const double SaturationKeyStep = 0.05;

    /// <summary>Wheel bitmaps generated once per pixel size and reused by every picker instance at
    /// that size (see <see cref="EnsureWheelBitmap"/>) - never regenerated per pixel or per drag
    /// move.</summary>
    private static readonly Dictionary<int, WriteableBitmap> WheelBitmapCache = [];

    private bool _isDragging;
    private bool _settingHexInternally;
    private HsvColor _hsv = HsvColor.FromRgb(RgbColor.White);

    public ColorWheelPicker()
    {
        InitializeComponent();

        Loaded += (_, _) => RefreshWheelAndThumb();
        SizeChanged += (_, _) => RefreshWheelAndThumb();

        WheelBorder.MouseLeftButtonDown += OnWheelMouseDown;
        WheelBorder.MouseMove += OnWheelMouseMove;
        WheelBorder.MouseLeftButtonUp += OnWheelMouseUp;
        WheelBorder.LostMouseCapture += OnWheelLostMouseCapture;
        WheelBorder.KeyDown += OnWheelKeyDown;

        ValueSlider.ValueChanged += OnValueSliderChanged;
        ValueSlider.PreviewMouseUp += (_, _) => FlushFinalValue();
        ValueSlider.KeyUp += (_, _) => FlushFinalValue();

        HexBox.KeyDown += OnHexBoxKeyDown;
        HexBox.LostFocus += (_, _) => CommitHexBoxText();

        SyncControlsFromHsv(raiseEvent: false, isFinal: true);
    }

    /// <summary>Raised on every interactive change (drag move, arrow key, value slider, or hex box
    /// commit). <see cref="ColorPickedEventArgs.IsFinal"/> is false only for a wheel/value-slider
    /// drag move that has not yet ended - everything else (key press, mouse up, hex commit) is
    /// final.</summary>
    public event EventHandler<ColorPickedEventArgs>? ColorPicked;

    /// <summary>The picked color as <c>#RRGGBB</c>. Two-way bindable; setting it externally (e.g.
    /// from a bound view model) updates the wheel, thumb, value slider and hex box to match without
    /// raising <see cref="ColorPicked"/> (that event is only for interaction that originates in
    /// this control).</summary>
    public string SelectedColorHex
    {
        get => (string)GetValue(SelectedColorHexProperty);
        set => SetValue(SelectedColorHexProperty, value);
    }

    private static void OnSelectedColorHexPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorWheelPicker picker && !picker._settingHexInternally && e.NewValue is string hex &&
            RgbColor.TryParse(hex, out var rgb))
        {
            picker._hsv = HsvColor.FromRgb(rgb);
            picker.SyncControlsFromHsv(raiseEvent: false, isFinal: true);
        }
    }

    private void RefreshWheelAndThumb()
    {
        EnsureWheelBitmap();
        UpdateThumbPosition();
    }

    private void EnsureWheelBitmap()
    {
        var size = (int)Math.Round(Math.Max(WheelBorder.ActualWidth, WheelBorder.Width));
        if (size <= 1)
        {
            return;
        }

        if (!WheelBitmapCache.TryGetValue(size, out var bitmap))
        {
            bitmap = GenerateWheelBitmap(size);
            WheelBitmapCache[size] = bitmap;
        }

        WheelImage.Source = bitmap;
    }

    /// <summary>Renders the hue/saturation disc (value fixed at 1 - the value slider is applied on
    /// top, not baked into the wheel) directly into a <see cref="WriteableBitmap"/>'s pixel buffer,
    /// once, rather than as per-pixel UI elements.</summary>
    private static WriteableBitmap GenerateWheelBitmap(int size)
    {
        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[size * size * 4];
        var radius = size / 2.0;

        for (var py = 0; py < size; py++)
        {
            for (var px = 0; px < size; px++)
            {
                var x = (px + 0.5 - radius) / radius;
                var y = (radius - (py + 0.5)) / radius; // screen Y grows down; math Y grows up.
                var distance = Math.Sqrt((x * x) + (y * y));
                var offset = ((py * size) + px) * 4;

                if (distance > 1.0)
                {
                    continue; // leave fully transparent outside the disc.
                }

                var (hue, saturation) = ColorWheelMath.PointToHueSaturation(x, y);
                var rgb = new HsvColor(hue, saturation, 1.0).ToRgb();

                // Anti-alias the outer ~2% so the rim isn't jagged.
                var alpha = distance > 0.98 ? Math.Clamp((1.0 - distance) / 0.02, 0, 1) : 1.0;

                pixels[offset] = rgb.B;
                pixels[offset + 1] = rgb.G;
                pixels[offset + 2] = rgb.R;
                pixels[offset + 3] = (byte)Math.Round(alpha * 255);
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private void UpdateThumbPosition()
    {
        var radius = WheelBorder.ActualWidth > 0 ? WheelBorder.ActualWidth / 2.0 : WheelBorder.Width / 2.0;
        if (radius <= 0)
        {
            return;
        }

        var (x, y) = ColorWheelMath.HueSaturationToPoint(_hsv.Hue, _hsv.Saturation);
        Canvas.SetLeft(Thumb, radius + (x * radius) - (Thumb.Width / 2));
        Canvas.SetTop(Thumb, radius - (y * radius) - (Thumb.Height / 2));
    }

    private void OnWheelMouseDown(object sender, MouseButtonEventArgs e)
    {
        WheelBorder.Focus();
        WheelBorder.CaptureMouse();
        _isDragging = true;
        UpdateFromPointer(e.GetPosition(WheelBorder), isFinal: false);
        e.Handled = true;
    }

    private void OnWheelMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateFromPointer(e.GetPosition(WheelBorder), isFinal: false);
    }

    private void OnWheelMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateFromPointer(e.GetPosition(WheelBorder), isFinal: true);
        EndDrag();
    }

    private void OnWheelLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        // Capture was stolen (e.g. the window lost focus mid-drag) - still send a final value so a
        // throttled/dropped intermediate color is not left un-applied.
        SyncControlsFromHsv(raiseEvent: true, isFinal: true);
        _isDragging = false;
    }

    private void EndDrag()
    {
        _isDragging = false;
        WheelBorder.ReleaseMouseCapture();
    }

    private void UpdateFromPointer(Point position, bool isFinal)
    {
        var radius = WheelBorder.ActualWidth / 2.0;
        if (radius <= 0)
        {
            return;
        }

        var x = (position.X - radius) / radius;
        var y = (radius - position.Y) / radius;
        var (hue, saturation) = ColorWheelMath.PointToHueSaturation(x, y);
        _hsv = _hsv with { Hue = hue, Saturation = saturation };
        SyncControlsFromHsv(raiseEvent: true, isFinal: isFinal);
    }

    private void OnWheelKeyDown(object sender, KeyEventArgs e)
    {
        double deltaHue = 0;
        double deltaSaturation = 0;
        switch (e.Key)
        {
            case Key.Left:
                deltaHue = -HueKeyStepDegrees;
                break;
            case Key.Right:
                deltaHue = HueKeyStepDegrees;
                break;
            case Key.Up:
                deltaSaturation = SaturationKeyStep;
                break;
            case Key.Down:
                deltaSaturation = -SaturationKeyStep;
                break;
            default:
                return;
        }

        _hsv = (_hsv with
        {
            Hue = _hsv.Hue + deltaHue,
            Saturation = Math.Clamp(_hsv.Saturation + deltaSaturation, 0, 1),
        }).Normalized();

        // A key press is a discrete, complete action - always final, never throttled.
        SyncControlsFromHsv(raiseEvent: true, isFinal: true);
        e.Handled = true;
    }

    private void OnValueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _hsv = _hsv with { Value = e.NewValue / 100.0 };
        SyncControlsFromHsv(raiseEvent: true, isFinal: false);
    }

    private void FlushFinalValue() => SyncControlsFromHsv(raiseEvent: true, isFinal: true);

    private void OnHexBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitHexBoxText();
            e.Handled = true;
        }
    }

    private void CommitHexBoxText()
    {
        if (RgbColor.TryParse(HexBox.Text, out var rgb))
        {
            _hsv = HsvColor.FromRgb(rgb);
            SyncControlsFromHsv(raiseEvent: true, isFinal: true);
        }
        else
        {
            // Invalid text - revert the box to the last known-good color instead of leaving it
            // showing something that will never parse.
            SyncControlsFromHsv(raiseEvent: false, isFinal: true);
        }
    }

    private void SyncControlsFromHsv(bool raiseEvent, bool isFinal)
    {
        var rgb = _hsv.ToRgb();
        var hex = rgb.ToHex();

        _settingHexInternally = true;
        SelectedColorHex = hex;
        _settingHexInternally = false;

        var brush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
        brush.Freeze();
        PreviewSwatch.Background = brush;

        if (!HexBox.IsKeyboardFocused)
        {
            HexBox.Text = hex;
        }

        if (!Equals(ValueSlider.Value, Math.Round(_hsv.Value * 100)))
        {
            ValueSlider.ValueChanged -= OnValueSliderChanged;
            ValueSlider.Value = Math.Round(_hsv.Value * 100);
            ValueSlider.ValueChanged += OnValueSliderChanged;
        }

        UpdateThumbPosition();

        if (raiseEvent)
        {
            ColorPicked?.Invoke(this, new ColorPickedEventArgs(rgb, isFinal));
        }
    }
}
