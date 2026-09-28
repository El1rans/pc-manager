using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Porchlight.App.Shell;

/// <summary>
/// Prevents a freshly shown window from painting as a solid white rectangle until the user clicks
/// it - a known WPF/Fluent-theme rendering gap, not an app bug in the traditional sense. Windows'
/// Desktop Window Manager (DWM) starts compositing a window's surface the moment
/// <see cref="Window.Show"/>/<see cref="Window.ShowDialog"/> makes it visible, but WPF's own
/// render thread does not hand DWM a painted frame until tens of milliseconds later. In that gap
/// the surface DWM is compositing from is unpainted, and an unpainted surface is white. Usually
/// the gap is too short to notice, but it can persist well past that - visibly white - until
/// something forces a repaint (most commonly a mouse click generating a fresh paint pass), which
/// is alarming for Porchlight's non-technical target users. Reproduced with
/// <c>Application.ThemeMode</c> set to both <c>System</c> and <c>Light</c>, so it is not specific
/// to dark/Mica rendering - see the investigation this fix is based on:
/// https://github.com/scjv/wpf-window-white-flash.
///
/// The fix asks DWM to keep the window fully hidden (<c>DWMWA_CLOAK</c>) from the moment its
/// native handle exists until WPF has actually presented a rendered frame, so DWM never has a
/// chance to composite the blank surface in the first place. This is the same technique used to
/// hide a window during other native-initialization windows (e.g. drag/drop thumbnails); it does
/// not skip or fake any painting; deliberately not "fixed" with a simulated click or a forced
/// repaint timer, which would just race the same underlying gap instead of removing it.
/// </summary>
public static class WhiteFlashGuard
{
    private const int DWMWA_CLOAK = 13;

    /// <summary>
    /// Attaches the cloak/uncloak guard to <paramref name="window"/>. Safe to call from the
    /// window's constructor - it only subscribes to lifecycle events fired later, when
    /// <see cref="Window.Show"/> or <see cref="Window.ShowDialog"/> is called.
    /// </summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += OnSourceInitialized;
        window.ContentRendered += OnContentRendered;
    }

    /// <summary>
    /// The native HWND exists as soon as <see cref="Window.SourceInitialized"/> fires, before DWM
    /// has shown or composited anything for it - cloak here, before <c>ShowWindow</c> gives DWM
    /// any surface to draw.
    /// </summary>
    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.SourceInitialized -= OnSourceInitialized;
        SetCloak(window, cloaked: true);
    }

    /// <summary>
    /// <see cref="Window.ContentRendered"/> fires once WPF's render thread has produced a frame
    /// for this window. One extra dispatch at <see cref="DispatcherPriority.Render"/> gives DWM's
    /// compositor a turn to actually present that frame before the window is uncloaked, so the
    /// first thing the user ever sees is the real, painted content - never the blank surface.
    /// </summary>
    private static void OnContentRendered(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.ContentRendered -= OnContentRendered;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Render, () => SetCloak(window, cloaked: false));
    }

    private static void SetCloak(Window window, bool cloaked)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            // Handle already gone (window closed before its first frame rendered) - nothing to do.
            return;
        }

        var value = cloaked ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref value, sizeof(int));
    }

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }
}
