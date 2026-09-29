using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="IRecycler"/>
public sealed partial class Recycler : IRecycler
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    private readonly ILogger<Recycler> _logger;

    public Recycler(ILogger<Recycler> logger)
    {
        _logger = logger;
    }

    public bool MoveToRecycleBin(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // FOF_ALLOWUNDO is what makes this a Recycle Bin move instead of a permanent delete, so
        // refuse anything that is not a plain, existing file first (never a folder or a link).
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            LogRefused(path);
            return false;
        }

        try
        {
            var operation = new ShFileOpStruct
            {
                wFunc = FoDelete,
                // The list must be double-null terminated; the marshaller adds the final null.
                pFrom = path + "\0",
                // Deliberately no FOF_NOCONFIRMATION (a deviation from the spec's flag list): when a file is
                // too big for the Recycle Bin, Windows would otherwise delete it permanently WITHOUT
                // asking. Without the flag it shows its own "delete permanently?" prompt in that
                // case, which is the safety net a Recycle-Bin-only feature needs.
                fFlags = (ushort)(FofAllowUndo | FofSilent | FofNoErrorUi),
            };

            var result = SHFileOperationW(ref operation);
            var succeeded = result == 0 && !operation.fAnyOperationsAborted;
            if (!succeeded)
            {
                LogFailed(path, result);
            }

            return succeeded;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            LogException(ex, path);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused to recycle {Path}: not an existing plain file.")]
    private partial void LogRefused(string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Moving {Path} to the Recycle Bin failed with code {Result}.")]
    private partial void LogFailed(string path, int result);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Moving {Path} to the Recycle Bin threw.")]
    private partial void LogException(Exception ex, string path);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }
}
