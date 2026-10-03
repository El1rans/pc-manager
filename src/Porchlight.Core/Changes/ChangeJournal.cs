using System.Text.Json;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Settings;

namespace Porchlight.Core.Changes;

/// <summary>
/// <see cref="IChangeJournal"/> backed by <c>change-journal.json</c> in the app data folder. Lazy
/// loaded, cached, every mutation under one lock, written atomically. A corrupt file is renamed to
/// <c>.bad</c> and the journal starts empty; a failed write keeps the entries in memory. Never
/// throws into the UI.
/// </summary>
public sealed partial class ChangeJournal : IChangeJournal
{
    public const string FileName = "change-journal.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly string _tempPath;
    private readonly ILogger<ChangeJournal> _logger;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, IChangeUndoer> _undoers;
    private readonly Lock _lock = new();
    private List<ChangeEntry>? _entries; // oldest first

    public ChangeJournal(ILogger<ChangeJournal> logger, IEnumerable<IChangeUndoer> undoers)
        : this(logger, undoers, TimeProvider.System, Path.Combine(AppDataPaths.Root, FileName))
    {
    }

    /// <summary>Test seam: own file, own clock.</summary>
    public ChangeJournal(ILogger<ChangeJournal> logger, IEnumerable<IChangeUndoer> undoers, TimeProvider time, string path)
    {
        _logger = logger;
        _time = time;
        _path = path;
        _tempPath = path + ".tmp";
        _undoers = undoers.ToDictionary(u => u.UndoType, StringComparer.Ordinal);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ChangeEntry> GetAll()
    {
        lock (_lock)
        {
            return [.. Load().AsEnumerable().Reverse()];
        }
    }

    public void Record(ChangeArea area, string description, string? undoType = null, string? undoPayload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        lock (_lock)
        {
            var list = Load();
            list.Add(new ChangeEntry(Guid.NewGuid(), _time.GetUtcNow(), area, description, undoType, undoPayload, null));
            ChangeJournalRules.Trim(list, _time.GetUtcNow());
            Save();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ChangeUndoResult> UndoAsync(Guid id, CancellationToken cancellationToken)
    {
        ChangeEntry? entry;
        lock (_lock)
        {
            entry = Load().FirstOrDefault(e => e.Id == id);
        }

        if (entry is null)
        {
            return ChangeUndoResult.Fail("This change is no longer in the list.");
        }

        if (entry.UndoneAt is not null)
        {
            return ChangeUndoResult.Fail("This change was already undone.");
        }

        if (entry.UndoType is null || entry.UndoPayload is null || !_undoers.TryGetValue(entry.UndoType, out var undoer))
        {
            return ChangeUndoResult.Fail("This change can't be undone.");
        }

        ChangeUndoResult result;
        try
        {
            result = await undoer.UndoAsync(entry.UndoPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LogUndoFailed(ex, entry.UndoType);
            return ChangeUndoResult.Fail("Couldn't undo this. Try again, or ask for help.");
        }

        if (!result.Succeeded)
        {
            return result;
        }

        lock (_lock)
        {
            var list = Load();
            var index = list.FindIndex(e => e.Id == id);
            if (index >= 0)
            {
                list[index] = list[index] with { UndoneAt = _time.GetUtcNow() };
                Save();
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private List<ChangeEntry> Load()
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
            _entries = JsonSerializer.Deserialize<List<ChangeEntry>>(json, JsonOptions) ?? [];
            _entries.RemoveAll(e => e is null || string.IsNullOrWhiteSpace(e.Description));
            _entries.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogUnreadable(ex, _path);
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
            LogMoveAsideFailed(ex, _path);
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
            LogSaveFailed(ex, _path);
            try
            {
                File.Delete(_tempPath);
            }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
            {
                LogSaveFailed(cleanupEx, _tempPath);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The change journal at {Path} couldn't be read; starting empty.")]
    private partial void LogUnreadable(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't rename the unreadable change journal {Path} to .bad.")]
    private partial void LogMoveAsideFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't save the change journal to {Path}; keeping it in memory.")]
    private partial void LogSaveFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Undoing a {UndoType} change failed.")]
    private partial void LogUndoFailed(Exception ex, string undoType);
}
