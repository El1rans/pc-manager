using System.Text.Json;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Winget;

/// <summary>
/// <see cref="IUpdateHistoryStore"/> backed by <c>update-history.json</c> next to
/// <c>settings.json</c> (its own file: it grows, and must not bloat or race with settings). Lazy
/// loaded, cached, every mutation under one lock, written atomically. A corrupt file is renamed to
/// <c>.bad</c> and history starts empty; a failed write keeps the in-memory list. Never throws into
/// the UI.
/// </summary>
public sealed class UpdateHistoryStore : IUpdateHistoryStore
{
    public const string FileName = "update-history.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly string _tempPath;
    private readonly ILogger<UpdateHistoryStore> _logger;
    private readonly Lock _lock = new();
    private List<UpdateHistoryEntry>? _entries;

    public UpdateHistoryStore(ILogger<UpdateHistoryStore> logger)
        : this(logger, Path.Combine(AppDataPaths.Root, FileName))
    {
    }

    /// <summary>Test seam: stores the history at <paramref name="path"/>.</summary>
    public UpdateHistoryStore(ILogger<UpdateHistoryStore> logger, string path)
    {
        _logger = logger;
        _path = path;
        _tempPath = path + ".tmp";
    }

    public IReadOnlyList<UpdateHistoryEntry> GetAll()
    {
        lock (_lock)
        {
            return [.. Load()];
        }
    }

    public void Add(UpdateHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            UpdateHistoryLog.Append(Load(), entry);
            Save();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Load().Clear();
            Save();
        }
    }

    private List<UpdateHistoryEntry> Load()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        _entries = [];
        if (!File.Exists(_path))
        {
            return _entries;
        }

        try
        {
            var json = File.ReadAllText(_path);
            _entries = JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Update history at {Path} couldn't be read; starting with an empty history.", _path);
            _entries = [];
            MoveAsideBadFile();
        }

        return _entries;
    }

    private void MoveAsideBadFile()
    {
        try
        {
            File.Move(_path, _path + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't rename the unreadable update history {Path} to .bad.", _path);
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_tempPath, JsonSerializer.Serialize(_entries, JsonOptions));
            File.Move(_tempPath, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't save the update history to {Path}; keeping it in memory.", _path);
            try
            {
                File.Delete(_tempPath);
            }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(cleanupEx, "Couldn't delete the temporary history file {Path}; harmless leftover.", _tempPath);
            }
        }
    }
}
