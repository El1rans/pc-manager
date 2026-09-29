using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Porchlight.App.Tray;

/// <summary>Win32 declarations for <see cref="TrayIcon"/> only. Plain <c>DllImport</c> (not the
/// <c>LibraryImport</c> generator) to avoid needing AllowUnsafeBlocks; fixed-size text buffers are
/// <see cref="InlineArrayAttribute"/> structs of <c>ushort</c> so the structs stay blittable.</summary>
#pragma warning disable SYSLIB1054
internal static class TrayNative
{
    internal const uint NIM_ADD = 0;
    internal const uint NIM_MODIFY = 1;
    internal const uint NIM_DELETE = 2;
    internal const uint NIM_SETVERSION = 4;

    internal const uint NIF_MESSAGE = 0x1;
    internal const uint NIF_ICON = 0x2;
    internal const uint NIF_TIP = 0x4;
    internal const uint NIF_INFO = 0x10;

    internal const uint NOTIFYICON_VERSION_4 = 4;
    internal const uint NIIF_INFO = 0x1;

    internal const int WM_CONTEXTMENU = 0x7B;
    internal const int WM_LBUTTONDBLCLK = 0x203;
    internal const int WM_NULL = 0;
    internal const int NIN_SELECT = 0x400;
    internal const int NIN_KEYSELECT = 0x401;
    internal const int NIN_BALLOONUSERCLICK = 0x405;

    internal const uint MF_STRING = 0x0;
    internal const uint MF_SEPARATOR = 0x800;
    internal const uint TPM_RETURNCMD = 0x100;
    internal const uint TPM_RIGHTBUTTON = 0x2;
    internal const uint TPM_BOTTOMALIGN = 0x20;

    /// <summary>Parent handle that makes a window message-only (invisible, no z-order).</summary>
    internal static readonly IntPtr HwndMessage = new(-3);

    [InlineArray(128)]
    internal struct Buffer128
    {
        private ushort _element;
    }

    [InlineArray(256)]
    internal struct Buffer256
    {
        private ushort _element;
    }

    [InlineArray(64)]
    internal struct Buffer64
    {
        private ushort _element;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NotifyIconData
    {
        public uint CbSize;
        public IntPtr Hwnd;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        public Buffer128 Tip;
        public uint State;
        public uint StateMask;
        public Buffer256 Info;
        public uint TimeoutOrVersion;
        public Buffer64 InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    /// <summary>Copies <paramref name="text"/> into a fixed buffer, truncating to
    /// <paramref name="maxChars"/> and leaving it zero-terminated.</summary>
    internal static void CopyText(Span<ushort> destination, string text, int maxChars)
    {
        destination.Clear();
        var count = Math.Min(text.Length, Math.Min(maxChars, destination.Length - 1));
        for (var i = 0; i < count; i++)
        {
            destination[i] = text[i];
        }
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode)]
    internal static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    internal static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
#pragma warning restore SYSLIB1054
