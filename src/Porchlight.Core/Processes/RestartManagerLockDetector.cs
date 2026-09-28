using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Processes;

/// <inheritdoc cref="IAppLockDetector"/>
/// <remarks>
/// Uses the Windows Restart Manager API (<c>rstrtmgr.dll</c> - <c>RmStartSession</c>,
/// <c>RmRegisterResources</c>, <c>RmGetList</c>) to ask Windows itself which processes have a
/// handle open on a bounded set of files under the install directory, rather than trying to guess
/// from process names. This is what Windows Explorer/Installer use for the same "these programs
/// need to close" dialog. Never terminates anything it finds - read-only. See
/// <c>docs/specs/09-friendly-update-outcomes.md</c>'s addendum: real case, OBS Studio blocked by
/// Chrome and another app holding <c>obs-virtualcam-module64.dll</c> open while OBS itself was not
/// running.
/// </remarks>
public sealed partial class RestartManagerLockDetector : IAppLockDetector
{
    /// <summary>Bound on how many files under the install directory are registered with the
    /// Restart Manager - enough to catch the app's own DLLs/EXEs without an unbounded scan of a
    /// large install tree.</summary>
    private const int MaxFilesToRegister = 64;

    /// <summary>Bound on how many distinct process names are returned - see
    /// <see cref="AppInUseExplanation.MaxNamedProcesses"/>; capped here too so a pathological case
    /// (dozens of handles) never even reaches the text builder.</summary>
    private const int MaxReturnedProcessNames = 5;

    private const int CchRmSessionKey = 32;
    private const int ErrorMoreData = 234;

    private static readonly string[] LockableExtensions = [".dll", ".exe"];

    private readonly ILogger<RestartManagerLockDetector> _logger;

    public RestartManagerLockDetector(ILogger<RestartManagerLockDetector> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> FindLockingProcessNames(string installLocation)
    {
        ArgumentException.ThrowIfNullOrEmpty(installLocation);

        try
        {
            if (!Directory.Exists(installLocation))
            {
                return [];
            }

            var files = EnumerateCandidateFiles(installLocation);
            return files.Count == 0 ? [] : QueryRestartManager(files);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Best-effort only - the caller (IAppInUseDiagnosticsService) falls back to the generic
            // "close the app" explanation when this returns empty. A native-interop or file-system
            // failure here must never break the update flow itself.
            LogLockQueryFailed(ex, installLocation);
            return [];
        }
    }

    /// <summary>Breadth-first, bounded walk of <paramref name="installLocation"/> collecting up to
    /// <see cref="MaxFilesToRegister"/> <c>*.dll</c>/<c>*.exe</c> files - the OBS Studio case this
    /// addendum was written for needed a subdirectory (<c>data\obs-plugins\win-dshow\</c>), so a
    /// top-level-only scan is not enough. A directory that can't be read (permissions, a reparse
    /// point, etc.) is skipped rather than failing the whole scan.</summary>
    private static List<string> EnumerateCandidateFiles(string installLocation)
    {
        var results = new List<string>();
        var directories = new Queue<string>();
        directories.Enqueue(installLocation);

        while (directories.Count > 0 && results.Count < MaxFilesToRegister)
        {
            var directory = directories.Dequeue();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (results.Count >= MaxFilesToRegister)
                {
                    break;
                }

                if (Directory.Exists(entry))
                {
                    directories.Enqueue(entry);
                }
                else if (LockableExtensions.Contains(Path.GetExtension(entry), StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(entry);
                }
            }
        }

        return results;
    }

    private static List<string> QueryRestartManager(List<string> files)
    {
        var sessionKey = new char[CchRmSessionKey + 1];
        if (RmStartSession(out var session, 0, sessionKey) != 0)
        {
            return [];
        }

        try
        {
            if (RmRegisterResources(session, (uint)files.Count, [.. files], 0, null, 0, null) != 0)
            {
                return [];
            }

            uint neededCount = 0;
            uint infoCount = 0;
            var firstPass = RmGetList(session, out neededCount, ref infoCount, null!, out _);
            if (firstPass != ErrorMoreData || neededCount == 0)
            {
                return [];
            }

            infoCount = neededCount;
            var processes = new RM_PROCESS_INFO[infoCount];
            if (RmGetList(session, out neededCount, ref infoCount, processes, out _) != 0)
            {
                return [];
            }

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in processes.Take((int)infoCount))
            {
                if (names.Count >= MaxReturnedProcessNames)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(process.strAppName) && seen.Add(process.strAppName))
                {
                    names.Add(process.strAppName);
                }
            }

            return names;
        }
        finally
        {
            _ = RmEndSession(session);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not query the Restart Manager for locked files under {InstallLocation}.")]
    private partial void LogLockQueryFailed(Exception ex, string installLocation);

    // --- rstrtmgr.dll P/Invoke --------------------------------------------------------------
    // Plain DllImport rather than the LibraryImport source generator: RM_PROCESS_INFO's fixed-size
    // inline character buffers and RmGetList's "call once to size, once to fill" array pattern are
    // exactly the shapes the generator's marshalling does not handle well - see the same tradeoff
    // already made in Monitoring/PerformanceSampler.cs.

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, char[] strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        out uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;

        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }
}
