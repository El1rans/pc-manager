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

    /// <summary>The current app-data folder, <c>%APPDATA%\Porchlight</c>.</summary>
    public static string NewDirectory { get; } = BuildDirectory(NewFolderName);

    /// <summary>The pre-rebrand app-data folder, <c>%APPDATA%\PCManager</c>.</summary>
    public static string LegacyDirectory { get; } = BuildDirectory(LegacyFolderName);

    /// <summary>
    /// Runs the migration for the real <see cref="NewDirectory"/>/<see cref="LegacyDirectory"/>
    /// pair. Called once, early in startup (see <c>App.OnStartup</c>) - before anything reads
    /// <c>settings.json</c> or the fan-control marker from the new location - so both classes' own
    /// default paths always see whatever this migrated.
    /// </summary>
    public static void MigrateIfNeeded(ILogger logger) => MigrateIfNeeded(logger, NewDirectory, LegacyDirectory);

    /// <summary>Test seam: lets tests point this at temp directories instead of the real
    /// <c>%APPDATA%</c> locations.</summary>
    internal static void MigrateIfNeeded(ILogger logger, string newDirectory, string legacyDirectory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(newDirectory);
        ArgumentException.ThrowIfNullOrEmpty(legacyDirectory);

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
            migratedAny |= CopyIfPresent(logger, legacyDirectory, newDirectory, SettingsFileName);
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
    /// prevents migrating the rest.</summary>
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
}
