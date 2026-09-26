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
            File.Move(tempPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
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
