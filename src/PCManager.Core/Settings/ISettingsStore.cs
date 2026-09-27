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

    /// <summary>
    /// Atomically persists <see cref="Current"/> as-is (write a temp file, then move it).
    /// Persistence is best-effort: if the file is transiently locked by another process (e.g.
    /// Defender, the Search indexer, OneDrive) the move is retried with bounded backoff, and if it
    /// still fails this logs a warning and returns without throwing. <see cref="Current"/> remains
    /// authoritative in memory regardless of whether the save succeeded; the next call to
    /// <see cref="Save"/> or <see cref="Update"/> retries persisting it.
    /// </summary>
    void Save();

    /// <summary>
    /// Atomically mutates <see cref="Current"/> and persists it, serialized with any other
    /// concurrent call to <see cref="Update"/> or <see cref="Save"/> so concurrent updates are
    /// never lost. The mutation always applies to <see cref="Current"/> even if persisting it
    /// fails; see <see cref="Save"/> for the best-effort persistence semantics. This never throws
    /// because of a transient file lock.
    /// </summary>
    void Update(Action<AppSettings> mutate);
}
