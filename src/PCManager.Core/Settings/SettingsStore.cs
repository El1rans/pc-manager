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

    private readonly ILogger<SettingsStore> _logger;
    private readonly string _settingsPath;

    public SettingsStore(ILogger<SettingsStore> logger)
        : this(logger, DefaultSettingsPath())
    {
    }

    /// <summary>Test seam: lets tests point the store at a temp directory.</summary>
    public SettingsStore(ILogger<SettingsStore> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
    }

    private static string DefaultSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PCManager",
            "settings.json");

    public AppSettings Load()
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
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read settings file at {Path}; using defaults.", _settingsPath);
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings file at {Path} is corrupt; backing it up and using defaults.", _settingsPath);
            BackupCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempPath = _settingsPath + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(_settingsPath))
        {
            File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _settingsPath);
        }
    }

    private void BackupCorruptFile()
    {
        try
        {
            var backupPath = _settingsPath + ".bak";
            File.Copy(_settingsPath, backupPath, overwrite: true);
        }
        catch (IOException ex)
        {
            // Best-effort backup; losing it does not prevent falling back to defaults.
            _logger.LogWarning(ex, "Could not back up corrupt settings file at {Path}.", _settingsPath);
        }
    }
}
