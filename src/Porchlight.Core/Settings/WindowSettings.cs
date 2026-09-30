namespace Porchlight.Core.Settings;

/// <summary>The main window's last size and position, restored at the next launch. All null until
/// the window has been closed or hidden at least once, in which case its XAML defaults apply.</summary>
public sealed class WindowSettings
{
    /// <summary>Left edge of the window's normal (not maximized) bounds, in device-independent
    /// pixels on the virtual screen.</summary>
    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    /// <summary>Whether the window was maximized; the bounds above are then the size it returns to
    /// when un-maximized.</summary>
    public bool IsMaximized { get; set; }
}
