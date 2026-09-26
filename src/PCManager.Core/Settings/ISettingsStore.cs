namespace PCManager.Core.Settings;

/// <summary>
/// Loads and persists <see cref="AppSettings"/> as JSON under
/// <c>%APPDATA%\PCManager\settings.json</c>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads settings from disk. Returns defaults when the file is missing; if the file is
    /// corrupt, it is backed up as <c>settings.json.bak</c> and defaults are returned.
    /// </summary>
    AppSettings Load();

    /// <summary>Atomically writes settings to disk (write a temp file, then replace).</summary>
    void Save(AppSettings settings);
}
