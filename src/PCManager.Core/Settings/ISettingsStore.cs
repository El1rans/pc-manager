namespace PCManager.Core.Settings;

/// <summary>
/// Holds the single, shared, in-memory copy of <see cref="AppSettings"/>, persisted as JSON at
/// <c>%APPDATA%\PCManager\settings.json</c>. Loaded once (on construction); features read and
/// write through this same instance instead of loading their own copy.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// The current, in-memory settings. Loaded from disk once, at construction; falls back to
    /// defaults when the file is missing or corrupt.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>Atomically persists <see cref="Current"/> as-is (write a temp file, then move it).</summary>
    void Save();

    /// <summary>
    /// Atomically mutates <see cref="Current"/> and persists it, serialized with any other
    /// concurrent call to <see cref="Update"/> or <see cref="Save"/> so concurrent updates are
    /// never lost.
    /// </summary>
    void Update(Action<AppSettings> mutate);
}
