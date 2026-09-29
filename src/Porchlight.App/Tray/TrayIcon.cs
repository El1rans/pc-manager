using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace Porchlight.App.Tray;

/// <summary>
/// <see cref="ITrayIcon"/> over <c>Shell_NotifyIcon</c>, hosted on a message-only window so it
/// needs neither WinForms nor a tray package (see <c>docs/specs/15-tray-and-alerts.md</c>, "Size").
/// Must be constructed on the UI thread. The icon is removed (<c>NIM_DELETE</c>) by
/// <see cref="Dispose"/> (host shutdown, <c>App.OnExit</c>), by <see cref="AppDomain.ProcessExit"/>,
/// and by the unhandled-exception path, and re-added when Explorer restarts
/// (<c>TaskbarCreated</c>).
/// </summary>
public sealed class TrayIcon : ITrayIcon
{
    /// <summary>Private callback message (WM_APP + 1) Windows posts icon events to.</summary>
    private const int CallbackMessage = 0x8001;

    private const uint IconId = 1;
    private const int TooltipMaxChars = 127;
    private const int BalloonTitleMaxChars = 63;
    private const int BalloonTextMaxChars = 255;

    private readonly Dispatcher _dispatcher;
    private readonly ILogger<TrayIcon> _logger;
    private readonly HwndSource _source;
    private readonly uint _taskbarCreatedMessage;
    private readonly Lock _gate = new();

    private IReadOnlyList<TrayMenuItem> _menu = [];
    private Action? _balloonClick;
    private string _tooltip = "Porchlight";
    private IntPtr _icon;
    private bool _added;
    private bool _disposed;

    public TrayIcon(ILogger<TrayIcon> logger)
    {
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _taskbarCreatedMessage = TrayNative.RegisterWindowMessage("TaskbarCreated");

        var parameters = new HwndSourceParameters("PorchlightTray")
        {
            ParentWindow = TrayNative.HwndMessage,
            WindowStyle = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _icon = LoadIcon();

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    public event EventHandler? DoubleClicked;

    public bool IsVisible
    {
        get
        {
            lock (_gate)
            {
                return _added && !_disposed;
            }
        }
    }

    public void Show(IReadOnlyList<TrayMenuItem> menu) => RunOnUi(() =>
    {
        _menu = menu;
        AddIcon();
    });

    public void SetTooltip(string text) => RunOnUi(() =>
    {
        _tooltip = text;
        if (_added)
        {
            var data = NewData(TrayNative.NIF_TIP);
            TrayNative.CopyText(data.Tip, text, TooltipMaxChars);
            TrayNative.Shell_NotifyIcon(TrayNative.NIM_MODIFY, ref data);
        }
    });

    public void ShowBalloon(string title, string message, Action? onClick) => RunOnUi(() =>
    {
        if (!_added)
        {
            return;
        }

        _balloonClick = onClick;
        var data = NewData(TrayNative.NIF_INFO);
        TrayNative.CopyText(data.Info, message, BalloonTextMaxChars);
        TrayNative.CopyText(data.InfoTitle, title, BalloonTitleMaxChars);
        data.InfoFlags = TrayNative.NIIF_INFO;
        if (!TrayNative.Shell_NotifyIcon(TrayNative.NIM_MODIFY, ref data))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogDebug("Shell_NotifyIcon could not show a balloon (error {Error}).", error);
            }
        }
    });

    /// <summary>Removes the icon and frees native resources. Idempotent; callable from any thread
    /// (<c>NIM_DELETE</c> is thread-safe; the message window is only destroyed when called on the
    /// owning thread - at process exit Windows reclaims it anyway).</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        RemoveIcon();

        if (_icon != IntPtr.Zero)
        {
            TrayNative.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        if (_dispatcher.CheckAccess())
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => RemoveIcon();

    private void RunOnUi(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.BeginInvoke(() =>
            {
                if (!_disposed)
                {
                    action();
                }
            });
        }
    }

    private void AddIcon()
    {
        var data = NewData(TrayNative.NIF_MESSAGE | TrayNative.NIF_ICON | TrayNative.NIF_TIP);
        data.CallbackMessage = CallbackMessage;
        data.Icon = _icon;
        TrayNative.CopyText(data.Tip, _tooltip, TooltipMaxChars);

        if (!TrayNative.Shell_NotifyIcon(TrayNative.NIM_ADD, ref data))
        {
            // Explorer may not be running yet (early sign-in); TaskbarCreated re-adds it later.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogInformation("Could not add the tray icon yet (error {Error}); will retry when the taskbar appears.", error);
            }
            return;
        }

        data.TimeoutOrVersion = TrayNative.NOTIFYICON_VERSION_4;
        TrayNative.Shell_NotifyIcon(TrayNative.NIM_SETVERSION, ref data);
        lock (_gate)
        {
            _added = true;
        }
    }

    private void RemoveIcon()
    {
        bool wasAdded;
        lock (_gate)
        {
            wasAdded = _added;
            _added = false;
        }

        if (!wasAdded)
        {
            return;
        }

        var data = NewData(0);
        TrayNative.Shell_NotifyIcon(TrayNative.NIM_DELETE, ref data);
    }

    private TrayNative.NotifyIconData NewData(uint flags) => new()
    {
        CbSize = (uint)Marshal.SizeOf<TrayNative.NotifyIconData>(),
        Hwnd = _source.Handle,
        Id = IconId,
        Flags = flags,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_disposed)
        {
            return IntPtr.Zero;
        }

        if (msg == _taskbarCreatedMessage)
        {
            // Explorer restarted: every notification icon was dropped, so add ours again.
            lock (_gate)
            {
                _added = false;
            }

            AddIcon();
            handled = true;
        }
        else if (msg == CallbackMessage)
        {
            OnIconEvent((int)(lParam.ToInt64() & 0xFFFF));
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnIconEvent(int iconMessage)
    {
        switch (iconMessage)
        {
            case TrayNative.WM_LBUTTONDBLCLK:
            case TrayNative.NIN_KEYSELECT:
                DoubleClicked?.Invoke(this, EventArgs.Empty);
                break;
            case TrayNative.WM_CONTEXTMENU:
                ShowMenu();
                break;
            case TrayNative.NIN_BALLOONUSERCLICK:
                RunSafely(_balloonClick, "balloon click");
                break;
        }
    }

    private void ShowMenu()
    {
        if (_menu.Count == 0)
        {
            return;
        }

        var handle = TrayNative.CreatePopupMenu();
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            for (var i = 0; i < _menu.Count; i++)
            {
                var item = _menu[i];
                if (item.IsSeparator)
                {
                    TrayNative.AppendMenu(handle, TrayNative.MF_SEPARATOR, UIntPtr.Zero, null);
                }
                else
                {
                    TrayNative.AppendMenu(handle, TrayNative.MF_STRING, (UIntPtr)(uint)(i + 1), item.Text);
                }
            }

            TrayNative.GetCursorPos(out var point);

            // Required so the menu closes when the user clicks elsewhere (documented Win32 quirk).
            TrayNative.SetForegroundWindow(_source.Handle);
            var chosen = TrayNative.TrackPopupMenu(
                handle,
                TrayNative.TPM_RETURNCMD | TrayNative.TPM_RIGHTBUTTON | TrayNative.TPM_BOTTOMALIGN,
                point.X,
                point.Y,
                0,
                _source.Handle,
                IntPtr.Zero);
            TrayNative.PostMessage(_source.Handle, TrayNative.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (chosen > 0 && chosen <= _menu.Count)
            {
                var action = _menu[chosen - 1].Invoke;
                // Off the menu's nested message loop, so a slow or modal action cannot stall it.
                _dispatcher.BeginInvoke(() => RunSafely(action, "menu item"));
            }
        }
        finally
        {
            TrayNative.DestroyMenu(handle);
        }
    }

    private void RunSafely(Action? action, string what)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            // A failing tray action must never take the app down from inside a window procedure.
            _logger.LogError(ex, "Tray {Action} failed.", what);
        }
    }

    private static IntPtr LoadIcon()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var small = new IntPtr[1];
            if (TrayNative.ExtractIconEx(path, 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
            {
                return small[0];
            }
        }

        return IntPtr.Zero;
    }
}
