using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Settings;

/// <summary>
/// One-time migration of a pre-rebrand install's <c>%APPDATA%\PCManager</c> folder to the new
/// <c>%APPDATA%\Porchlight</c> folder used since the product was renamed from "PC Manager" to
/// "Porchlight" (docs/specs/08-rebrand-porchlight.md). Copies only the files the app actually
/// reads at startup (<c>settings.json</c> and the fan-control activity marker) - never the whole
/// folder - and never touches or deletes anything under the legacy folder: an upgrade must not
/// risk losing a family member's settings.
/// </summary>
public static partial class AppDataMigrator
{
    private const string NewFolderName = "Porchlight";
    private const string LegacyFolderName = "PCManager";
    private const string SettingsFileName = "settings.json";
    private const string FanControlMarkerFileName = "fancontrol.active";

    /// <summary>
    /// Bounded backoff (milliseconds) between retries of the <c>settings.json</c> copy only - see
    /// <see cref="MigrateIfNeeded(ILogger)"/>'s remarks. Mirrors <c>SettingsStore.MoveRetryDelaysMs</c>:
    /// Defender, the Search indexer, or OneDrive/AV filter drivers can briefly hold the just-written
    /// legacy file. The fan-control marker is not worth retrying - losing it only means a later
    /// launch cannot warn about an unclean shutdown, never lost user settings.
    /// </summary>
    private static readonly int[] SettingsCopyRetryDelaysMs = [100, 100, 150, 150, 200];

    /// <summary>The current app-data folder, <c>%APPDATA%\Porchlight</c>.</summary>
    public static string NewDirectory { get; } = BuildDirectory(NewFolderName);

    /// <summary>The pre-rebrand app-data folder, <c>%APPDATA%\PCManager</c>.</summary>
    public static string LegacyDirectory { get; } = BuildDirectory(LegacyFolderName);

    /// <summary>
    /// Runs the migration for the real <see cref="NewDirectory"/>/<see cref="LegacyDirectory"/>
    /// pair. Called once, early in startup (see <c>App.OnStartup</c>) - before anything reads
    /// <c>settings.json</c> or the fan-control marker from the new location - so both classes' own
    /// default paths always see whatever this migrated. Must also run before the first Serilog
    /// write of the "real" (non-bootstrap) session: that first write creates
    /// <c>%APPDATA%\Porchlight\logs</c> itself, which would make <see cref="NewDirectory"/> already
    /// exist and skip migration entirely - see the call site in <c>App.OnStartup</c>.
    /// </summary>
    public static void MigrateIfNeeded(ILogger logger) =>
        MigrateIfNeeded(logger, NewDirectory, LegacyDirectory, Thread.Sleep);

    /// <summary>Test seam: lets tests point this at temp directories instead of the real
    /// <c>%APPDATA%</c> locations, with the real <see cref="Thread.Sleep(TimeSpan)"/> backoff.</summary>
    internal static void MigrateIfNeeded(ILogger logger, string newDirectory, string legacyDirectory) =>
        MigrateIfNeeded(logger, newDirectory, legacyDirectory, Thread.Sleep);

    /// <summary>Test seam: also replaces the real <see cref="Thread.Sleep(TimeSpan)"/> backoff
    /// between <c>settings.json</c> copy retries with a deterministic action - e.g. one that
    /// releases a file lock held by the test itself on a later attempt, instead of racing a real
    /// timer against the retry budget.</summary>
    internal static void MigrateIfNeeded(
        ILogger logger, string newDirectory, string legacyDirectory, Action<TimeSpan> sleeper)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(newDirectory);
        ArgumentException.ThrowIfNullOrEmpty(legacyDirectory);
        ArgumentNullException.ThrowIfNull(sleeper);

        try
        {
            // Both a fresh install (neither folder exists yet) and "both already exist" (a second
            // launch after migration already ran, or a user who somehow has both) take this same
            // early return - the new folder always wins once it exists, and nothing here ever
            // overwrites it.
            if (!Directory.Exists(legacyDirectory) || Directory.Exists(newDirectory))
            {
                return;
            }

            Directory.CreateDirectory(newDirectory);

            var migratedAny = false;
            migratedAny |= CopyWithRetry(
                logger, legacyDirectory, newDirectory, SettingsFileName, sleeper, SettingsCopyRetryDelaysMs);
            migratedAny |= CopyIfPresent(logger, legacyDirectory, newDirectory, FanControlMarkerFileName);

            if (migratedAny)
            {
                LogMigrated(logger, legacyDirectory, newDirectory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a failed migration must not stop the app from starting - worst case it
            // starts as if this were a fresh install, and the user's original data under the
            // legacy folder is untouched either way.
            LogMigrationFailed(logger, ex, legacyDirectory, newDirectory);
        }
    }

    /// <summary>Copies one file from <paramref name="legacyDirectory"/> to
    /// <paramref name="newDirectory"/> if the source exists and the destination does not.
    /// Failures are logged and swallowed so one bad file (e.g. locked by another process) never
    /// prevents migrating the rest. No retry - used for the fan-control marker only, where losing
    /// it is not worth a retry loop.</summary>
    private static bool CopyIfPresent(ILogger logger, string legacyDirectory, string newDirectory, string fileName)
    {
        var source = Path.Combine(legacyDirectory, fileName);
        var destination = Path.Combine(newDirectory, fileName);

        if (!File.Exists(source) || File.Exists(destination))
        {
            return false;
        }

        try
        {
            File.Copy(source, destination);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFileCopyFailed(logger, ex, fileName, legacyDirectory);
            return false;
        }
    }

    /// <summary>
    /// Copies <c>settings.json</c> with bounded backoff (<paramref name="retryDelaysMs"/>) on
    /// transient <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> failures -
    /// without this, a single transient lock (Defender, the Search indexer, OneDrive) at the exact
    /// moment of migration would silently start the user on a brand-new, empty settings file with
    /// no later retry (the new folder already exists by then, so <see cref="MigrateIfNeeded(ILogger)"/>
    /// never runs again). If every attempt fails, logs an <see cref="LogLevel.Error"/> (not just a
    /// warning - unlike the fan-control marker, this is real user data) and returns without
    /// throwing; the legacy file is left untouched either way.
    /// </summary>
    private static bool CopyWithRetry(
        ILogger logger,
        string legacyDirectory,
        string newDirectory,
        string fileName,
        Action<TimeSpan> sleeper,
        int[] retryDelaysMs)
    {
        var source = Path.Combine(legacyDirectory, fileName);
        var destination = Path.Combine(newDirectory, fileName);

        if (!File.Exists(source) || File.Exists(destination))
        {
            return false;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Copy(source, destination);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= retryDelaysMs.Length)
                {
                    LogSettingsCopyFailedPermanently(logger, ex, fileName, legacyDirectory, attempt + 1);
                    return false;
                }

                sleeper(TimeSpan.FromMilliseconds(retryDelaysMs[attempt]));
            }
        }
    }

    private static string BuildDirectory(string folderName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folderName);

    // Source-generated (guarded by IsEnabled internally) so the message is never formatted when
    // the relevant log level is disabled - see CA1873.
    [LoggerMessage(Level = LogLevel.Information, Message = "Migrated settings from the legacy folder {LegacyDirectory} to {NewDirectory} (renamed from PC Manager to Porchlight).")]
    private static partial void LogMigrated(ILogger logger, string legacyDirectory, string newDirectory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not migrate settings from the legacy folder {LegacyDirectory} to {NewDirectory}.")]
    private static partial void LogMigrationFailed(ILogger logger, Exception exception, string legacyDirectory, string newDirectory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not migrate {FileName} from the legacy folder {LegacyDirectory}.")]
    private static partial void LogFileCopyFailed(ILogger logger, Exception exception, string fileName, string legacyDirectory);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not migrate {FileName} from the legacy folder {LegacyDirectory} after {Attempts} attempts; the user's settings were NOT migrated to the new location.")]
    private static partial void LogSettingsCopyFailedPermanently(ILogger logger, Exception exception, string fileName, string legacyDirectory, int attempts);
}
