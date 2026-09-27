using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Settings;

/// <inheritdoc cref="ISettingsStore"/>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Bounded backoff (milliseconds) between retries of the final <see cref="File.Move"/> in
    /// <see cref="SaveToDisk"/>. Defender, the Search indexer, or OneDrive/AV filter drivers can
    /// briefly hold the just-written target file, which surfaces as a transient
    /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    private static readonly int[] MoveRetryDelaysMs = [10, 25, 50, 100, 200];

    private readonly Lock _lock = new();
    private readonly ILogger<SettingsStore> _logger;
    private readonly string _settingsPath;
    private readonly AppSettings _current;

    public SettingsStore(ILogger<SettingsStore> logger)
        : this(logger, DefaultSettingsPath())
    {
    }

    /// <summary>Test seam: lets tests point the store at a temp directory.</summary>
    public SettingsStore(ILogger<SettingsStore> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
        _current = LoadFromDisk();
    }

    public AppSettings Current => _current;

    private static string DefaultSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PCManager",
            "settings.json");

    public void Save()
    {
        lock (_lock)
        {
            SaveToDisk(_current);
        }
    }

    public void Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        lock (_lock)
        {
            mutate(_current);
            SaveToDisk(_current);
        }
    }

    private AppSettings LoadFromDisk()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        string json;
        try
        {
            json = File.ReadAllText(_settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read settings file at {Path}; using defaults.", _settingsPath);
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            NormalizeSections(settings);
            return settings;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings file at {Path} is corrupt; backing it up and using defaults.", _settingsPath);
            BackupCorruptFile();
            return new AppSettings();
        }
    }

    /// <summary>
    /// An explicit JSON <c>null</c> for a section (e.g. <c>{"Updates": null}</c>) overwrites the
    /// property initializer during deserialization; put defaults back so callers never see a null
    /// section.
    /// </summary>
    private static void NormalizeSections(AppSettings settings)
    {
        settings.Updates ??= new UpdatesSettings();
        settings.Hardware ??= new HardwareSettings();
        settings.Lighting ??= new LightingSettings();
        settings.RemoteSupport ??= new RemoteSupportSettings();
        settings.Setup ??= new SetupSettings();
    }

    /// <summary>
    /// Writes <paramref name="settings"/> to a temp file, then atomically moves it onto
    /// <see cref="_settingsPath"/>. Persistence is best-effort: if the final move keeps failing
    /// because some other process (Defender, the Search indexer, OneDrive) is transiently holding
    /// the target, this logs a warning and returns instead of throwing, so a lock held by another
    /// process never crashes the caller. <see cref="Current"/> stays authoritative in memory and
    /// the next successful save will persist it.
    /// </summary>
    private void SaveToDisk(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tempPath, json);
            MoveWithRetry(tempPath, _settingsPath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup; a leftover temp file does not affect correctness and must
                // never mask the original save outcome.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Could not delete temp settings file at {Path}.", tempPath);
                }
            }
        }
    }

    /// <summary>
    /// Moves <paramref name="tempPath"/> onto <paramref name="targetPath"/>, retrying with bounded
    /// backoff (<see cref="MoveRetryDelaysMs"/>) on transient <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> failures. If every attempt fails, logs a warning
    /// and returns without throwing; the save is best-effort and the next call retries.
    /// </summary>
    private void MoveWithRetry(string tempPath, string targetPath)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tempPath, targetPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= MoveRetryDelaysMs.Length)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not save settings to {Path} after {Attempts} attempts; keeping in-memory settings and retrying on the next save.",
                        targetPath,
                        attempt + 1);
                    return;
                }

                Thread.Sleep(MoveRetryDelaysMs[attempt]);
            }
        }
    }

    private void BackupCorruptFile()
    {
        try
        {
            var backupPath = _settingsPath + ".bak";
            File.Copy(_settingsPath, backupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort backup; losing it does not prevent falling back to defaults.
            _logger.LogWarning(ex, "Could not back up corrupt settings file at {Path}.", _settingsPath);
        }
    }
}
