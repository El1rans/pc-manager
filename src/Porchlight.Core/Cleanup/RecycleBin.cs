using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IRecycleBin"/>
public sealed partial class RecycleBin : IRecycleBin
{
    private const uint ShErbNoConfirmation = 0x1;
    private const uint ShErbNoProgressUi = 0x2;
    private const uint ShErbNoSound = 0x4;

    private readonly ILogger<RecycleBin> _logger;

    public RecycleBin(ILogger<RecycleBin> logger)
    {
        _logger = logger;
    }

    public RecycleBinInfo QuerySize()
    {
        try
        {
            var info = new ShQueryRecycleBinInfo { cbSize = (uint)Marshal.SizeOf<ShQueryRecycleBinInfo>() };
            var result = SHQueryRecycleBinW(null, ref info);
            return result == 0 ? new RecycleBinInfo(info.i64Size, info.i64NumItems) : new RecycleBinInfo(0, 0);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            // Treated as "empty" so the page simply shows 0 B for this category.
            LogQueryFailed(ex);
            return new RecycleBinInfo(0, 0);
        }
    }

    public bool Empty()
    {
        try
        {
            var result = SHEmptyRecycleBinW(IntPtr.Zero, null, ShErbNoConfirmation | ShErbNoProgressUi | ShErbNoSound);
            if (result != 0)
            {
                LogEmptyReturned(result);
            }

            return result == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            LogEmptyFailed(ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not query the Recycle Bin size.")]
    private partial void LogQueryFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Emptying the Recycle Bin returned 0x{Result:X8} (already empty, or Windows declined).")]
    private partial void LogEmptyReturned(int result);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not empty the Recycle Bin.")]
    private partial void LogEmptyFailed(Exception ex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBinW(string? pszRootPath, ref ShQueryRecycleBinInfo pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ShQueryRecycleBinInfo
    {
        public uint cbSize;
        public long i64Size;
        public long i64NumItems;
    }
}
