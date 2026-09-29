using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Cleanup;

/// <inheritdoc cref="ICleanupPathProvider"/>
public sealed partial class CleanupPathProvider : ICleanupPathProvider
{
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    private readonly ILogger<CleanupPathProvider> _logger;

    public CleanupPathProvider(ILogger<CleanupPathProvider> logger)
    {
        _logger = logger;
    }

    public string TempPath => Path.GetTempPath();

    public string WindowsDirectory => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    public string ProgramFilesX86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public string DownloadsFolder
    {
        get
        {
            var known = TryGetKnownFolder(DownloadsFolderId);
            if (!string.IsNullOrEmpty(known))
            {
                return known;
            }

            var profile = UserProfile;
            return string.IsNullOrEmpty(profile) ? string.Empty : Path.Combine(profile, "Downloads");
        }
    }

    public IReadOnlyList<string> PersonalFolders =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        DownloadsFolder,
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
    ];

    private string? TryGetKnownFolder(Guid folderId)
    {
        try
        {
            var result = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out var pathPointer);
            try
            {
                return result == 0 ? Marshal.PtrToStringUni(pathPointer) : null;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            // Falls back to <profile>\Downloads, which is where Downloads is unless redirected.
            LogKnownFolderFailed(ex, folderId);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not resolve known folder {FolderId}; using the default location.")]
    private partial void LogKnownFolderFailed(Exception ex, Guid folderId);

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
