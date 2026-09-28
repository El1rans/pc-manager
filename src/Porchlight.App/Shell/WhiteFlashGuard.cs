using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
///
/// Cloaking is only ever half the story: a window that is cloaked and never uncloaked would be
/// invisible (but still present in the taskbar) forever, which is worse for a non-technical user
/// than the white flash this exists to prevent. <see cref="CloakLifecycle"/> guarantees an uncloak
/// no matter what goes wrong on the render path, via a bounded fallback timer; this class also
/// forces an uncloak on the window's state changing (e.g. restored from minimized) or closing, and
/// exposes <see cref="UncloakAll"/> for an app-wide crash sweep.
/// </summary>
public static class WhiteFlashGuard
{
    private const int DWMWA_CLOAK = 13;

    /// <summary>
    /// Upper bound on how long a window can stay cloaked waiting for its first real frame. The
    /// render gap this guards against is normally tens of milliseconds at most (see the class
    /// remarks above), so this never fires on a healthy launch; it exists purely so a window that
    /// never reaches <see cref="Window.ContentRendered"/> at all - an exception during its first
    /// layout/render pass, the window being shown minimized, or anything else that skips a normal
    /// render - reappears within a bound a user would read as "slow", not "frozen forever".
    /// </summary>
    internal static readonly TimeSpan FallbackDelay = TimeSpan.FromMilliseconds(1500);

    private static readonly object RegistryLock = new();

    /// <summary>Every window this guard has successfully cloaked and not yet uncloaked. Lets
    /// <see cref="UncloakAll"/> sweep all of them without needing a reference to any specific
    /// window - e.g. from the app's unhandled-exception handler, which does not know (and should
    /// not need to know) which window, if any, is currently cloaked.</summary>
    private static readonly List<CloakLifecycle> Registry = [];

    /// <summary>
    /// Attaches the cloak/uncloak guard to <paramref name="window"/>. Safe to call from the
    /// window's constructor - it only subscribes to lifecycle events fired later, when
    /// <see cref="Window.Show"/> or <see cref="Window.ShowDialog"/> is called.
    /// </summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += OnSourceInitialized;
        return;

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;

            // The native HWND exists as soon as SourceInitialized fires, before DWM has shown or
            // composited anything for it - cloak here, before ShowWindow gives DWM any surface to
            // draw.
            if (!TrySetCloak(window, cloaked: true))
            {
                // Cloaking itself failed (DwmSetWindowAttribute returned an error - see
                // TrySetCloak): nothing was hidden, so there is nothing to guard and nothing to
                // track. The window just renders the way it always would have, white flash and
                // all - worse than the fix, but never worse than not having attempted it.
                return;
            }

            CloakLifecycle? lifecycle = null;
            lifecycle = new CloakLifecycle(TimeProvider.System, FallbackDelay, () => Uncloak(window, lifecycle!));

            lock (RegistryLock)
            {
                Registry.Add(lifecycle);
            }

            window.ContentRendered += OnContentRendered;
            window.StateChanged += OnStateChanged;
            window.Closing += OnClosing;

            void OnContentRendered(object? contentSender, EventArgs contentArgs)
            {
                window.ContentRendered -= OnContentRendered;

                // ContentRendered fires once WPF's render thread has produced a frame. One extra
                // dispatch at Render priority gives DWM's compositor a turn to actually present
                // that frame before uncloaking, so the first thing the user ever sees is the real,
                // painted content - never the blank surface.
                window.Dispatcher.BeginInvoke(DispatcherPriority.Render, () => lifecycle.OnContentRendered());
            }

            // The window becoming minimized/restored (most commonly: shown minimized, so it never
            // renders a normal frame at all) should not leave it cloaked once it is no longer
            // minimized - force an uncloak regardless of whether ContentRendered ever fired.
            void OnStateChanged(object? stateSender, EventArgs stateArgs) => lifecycle.ForceUncloak();

            // Closing before the first frame rendered (e.g. the user or the app closes the window
            // during startup) must not leave a stale cloaked entry sitting in the registry, and
            // must not leave the window invisible if something cancels the close.
            void OnClosing(object? closingSender, CancelEventArgs closingArgs) => lifecycle.ForceUncloak();
        }
    }

    /// <summary>
    /// Forces every window this guard has cloaked back to visible, regardless of render state.
    /// Intended to be called from the app's unhandled-exception handling: if the exception
    /// happened during a window's own first layout/render pass, that window could otherwise still
    /// be cloaked when an error dialog owned by it is shown, hiding the dialog along with it.
    /// </summary>
    public static void UncloakAll()
    {
        CloakLifecycle[] snapshot;
        lock (RegistryLock)
        {
            snapshot = [.. Registry];
        }

        foreach (var lifecycle in snapshot)
        {
            lifecycle.ForceUncloak();
        }
    }

    private static void Uncloak(Window window, CloakLifecycle lifecycle)
    {
        void DoUncloak()
        {
            TrySetCloak(window, cloaked: false);
            lock (RegistryLock)
            {
                Registry.Remove(lifecycle);
            }
        }

        // The DWM call itself has no thread affinity, but WindowInteropHelper/Window do - always
        // reach them from the UI thread, whether this was called from a threadpool fallback-timer
        // callback or already on the dispatcher.
        if (window.Dispatcher.CheckAccess())
        {
            DoUncloak();
        }
        else
        {
            window.Dispatcher.BeginInvoke((Action)DoUncloak);
        }
    }

    private static bool TrySetCloak(Window window, bool cloaked)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            // Handle already gone (window closed before its first frame rendered) - nothing to do.
            return false;
        }

        var value = cloaked ? 1 : 0;
        var hresult = NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref value, sizeof(int));
        if (hresult != 0)
        {
            // Best-effort: a failure here means the window renders exactly as it would have
            // without this guard, not that anything is broken - log and move on rather than
            // throwing out of a SourceInitialized/uncloak handler.
            var logger = App.Services?.GetService<ILogger<CloakLifecycle>>();
            if (logger is { } && logger.IsEnabled(LogLevel.Debug))
            {
                var windowName = window.GetType().Name;
                logger.LogDebug(
                    "DwmSetWindowAttribute(DWMWA_CLOAK, {Cloaked}) failed with HRESULT 0x{HResult:X8} for {Window} - continuing without the white-flash guard for this window.",
                    cloaked, hresult, windowName);
            }

            return false;
        }

        return true;
    }

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }
}
