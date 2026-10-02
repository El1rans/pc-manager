using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="IStartupService"/>
public sealed partial class StartupService : IStartupService
{
    private static readonly StartupSource[] RunSources =
        [StartupSource.CurrentUserRun, StartupSource.MachineRun, StartupSource.MachineRun32];

    private static readonly StartupSource[] FolderSources =
        [StartupSource.CurrentUserFolder, StartupSource.MachineFolder];

    private readonly IStartupRegistry _registry;
    private readonly IStartupFolderReader _folders;
    private readonly IFileProductInfoReader _fileInfo;
    private readonly IStartupInfoReader _startupInfo;
    private readonly ILogonTaskSource _tasks;
    private readonly IElevationService _elevation;
    private readonly ILogger<StartupService> _logger;
    private readonly TimeProvider _timeProvider;
    private const string MicrosoftTaskFolder = @"\Microsoft\";
    private readonly string _windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // Ids handed out by the last ListAsync; SetEnabledAsync refuses anything else.
    private volatile Dictionary<string, StartupEntry> _known = [];
    private volatile bool _impactNeedsAdmin;

    public StartupService(
        IStartupRegistry registry,
        IStartupFolderReader folders,
        IFileProductInfoReader fileInfo,
        IStartupInfoReader startupInfo,
        ILogonTaskSource tasks,
        IElevationService elevation,
        ILogger<StartupService> logger,
        TimeProvider? timeProvider = null)
    {
        _registry = registry;
        _folders = folders;
        _fileInfo = fileInfo;
        _startupInfo = startupInfo;
        _tasks = tasks;
        _elevation = elevation;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool ImpactNeedsAdmin => _impactNeedsAdmin;

    public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<StartupEntry>>(() => ListCore(cancellationToken), cancellationToken);

    public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken) =>
        Task.Run(() => SetEnabledCore(entryId, enabled), cancellationToken);

    private List<StartupEntry> ListCore(CancellationToken cancellationToken)
    {
        var entries = new List<StartupEntry>();

        foreach (var source in RunSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<StartupRunValue> values;
            try
            {
                values = _registry.ReadRunValues(source);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                LogRunListUnreadable(ex, source);
                continue;
            }

            entries.AddRange(values.Select(v => BuildEntry(source, v.Name, v.Command, StartupCommandParser.ExtractExecutablePath(v.Command))));
        }

        foreach (var source in FolderSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<StartupFolderItem> items;
            try
            {
                items = _folders.ReadFolder(source);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                LogFolderUnreadable(ex, source);
                continue;
            }

            entries.AddRange(items.Select(i => BuildEntry(source, i.FileName, i.TargetPath ?? i.FileName, i.TargetPath)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        entries.AddRange(ReadLogonTaskEntries());

        AttachImpact(entries);
        entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        _known = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
        return entries;
    }

    private List<StartupEntry> ReadLogonTaskEntries()
    {
        try
        {
            return [.. _tasks.ReadLogonTasks().Select(BuildTaskEntry)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or COMException)
        {
            LogTasksUnreadable(ex);
            return [];
        }
    }

    private StartupEntry BuildTaskEntry(LogonTask task)
    {
        var path = task.ExecutablePath is not null && Path.IsPathRooted(task.ExecutablePath) ? task.ExecutablePath : null;
        var info = path is null ? null : _fileInfo.Read(path);

        var displayName = FirstNonBlank(info?.Description, info?.Product, task.Name) ?? task.Name;
        var publisher = FirstNonBlank(info?.Company);
        var classification = StartupClassifier.Classify(task.Name, path ?? task.ExecutablePath, publisher, info?.Description, _windowsDirectory);

        // Microsoft's own (non-Windows) tasks, e.g. \Microsoft\Office\..., are worth keeping.
        var keep = classification.RecommendedToKeep ||
                   task.Path.StartsWith(MicrosoftTaskFolder, StringComparison.OrdinalIgnoreCase);

        return new StartupEntry(
            $"{StartupSource.LogonTask}|{task.Path}", StartupSource.LogonTask, task.Path, displayName.Trim(), publisher?.Trim(),
            path, classification.Hint, keep, task.IsEnabled, IsMachineWide: task.IsMachineWide);
    }

    private void AttachImpact(List<StartupEntry> entries)
    {
        var trace = _startupInfo.Read();
        _impactNeedsAdmin = trace.AccessDenied;
        if (trace.Records.Count == 0)
        {
            return;
        }

        var rater = new StartupImpactRater(trace.Records);
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i] = entries[i] with { Impact = rater.Rate(entries[i].ExecutablePath) };
        }
    }

    private StartupEntry BuildEntry(StartupSource source, string itemName, string command, string? candidatePath)
    {
        // A relative name like "rundll32.exe" is not looked up: only a full path is inspected.
        var path = candidatePath is not null && Path.IsPathRooted(candidatePath) ? candidatePath : null;
        var info = path is null ? null : _fileInfo.Read(path);

        var displayName = FirstNonBlank(
            info?.Description,
            info?.Product,
            source.IsFolder() ? Path.GetFileNameWithoutExtension(itemName) : itemName) ?? itemName;
        var publisher = FirstNonBlank(info?.Company);
        var classification = StartupClassifier.Classify(itemName, path ?? command, publisher, info?.Description, _windowsDirectory);

        var enabled = true;
        try
        {
            enabled = StartupApprovedBlob.IsEnabled(_registry.ReadApproval(source, itemName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            LogStateUnreadable(ex, itemName);
        }

        return new StartupEntry(
            $"{source}|{itemName}", source, itemName, displayName.Trim(), publisher?.Trim(), path,
            classification.Hint, classification.RecommendedToKeep, enabled);
    }

    private StartupChangeResult SetEnabledCore(string entryId, bool enabled)
    {
        if (!_known.TryGetValue(entryId, out var entry))
        {
            return StartupChangeResult.NotFound;
        }

        // Safety rule: per-machine items are only ever changed when running elevated.
        if (entry.RequiresAdmin && !_elevation.IsElevated)
        {
            return StartupChangeResult.NeedsAdmin;
        }

        if (entry.Source == StartupSource.LogonTask)
        {
            return SetTaskEnabled(entry, enabled);
        }

        try
        {
            var blob = enabled
                ? StartupApprovedBlob.CreateEnabled()
                : StartupApprovedBlob.CreateDisabled(_timeProvider.GetUtcNow());
            _registry.WriteApproval(entry.Source, entry.ItemName, blob);
            return StartupChangeResult.Changed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            LogChangeFailed(ex, entry.ItemName);

            return StartupChangeResult.Failed;
        }
    }

    private StartupChangeResult SetTaskEnabled(StartupEntry entry, bool enabled)
    {
        try
        {
            _tasks.SetEnabled(entry.ItemName, enabled);
            return StartupChangeResult.Changed;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogTaskAccessDenied(ex, entry.ItemName);
            return StartupChangeResult.NeedsAdmin;
        }
        catch (Exception ex) when (ex is IOException or SecurityException or COMException)
        {
            LogChangeFailed(ex, entry.ItemName);
            return StartupChangeResult.Failed;
        }
    }

    // Source-generated so nothing is formatted when the level is off (CA1873).
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the {Source} startup list; skipping it.")]
    private partial void LogRunListUnreadable(Exception ex, StartupSource source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the {Source} startup folder; skipping it.")]
    private partial void LogFolderUnreadable(Exception ex, StartupSource source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the on/off state of {Item}; assuming it is on.")]
    private partial void LogStateUnreadable(Exception ex, string item);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the scheduled tasks; skipping them.")]
    private partial void LogTasksUnreadable(Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Access to the scheduled task {Item} was denied; it needs administrator rights.")]
    private partial void LogTaskAccessDenied(Exception ex, string item);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not change the startup setting of {Item}.")]
    private partial void LogChangeFailed(Exception ex, string item);

    private static string? FirstNonBlank(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
