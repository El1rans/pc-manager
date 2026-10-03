using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Processes;
using Porchlight.Core.Winget;

namespace Porchlight.Core.RemoveApps;

/// <inheritdoc cref="IRemoveAppsService"/>
public sealed partial class RemoveAppsService : IRemoveAppsService
{
    private readonly IInstalledAppsReader _reader;
    private readonly IWingetClient _winget;
    private readonly IAppUninstaller _uninstaller;
    private readonly ILogger<RemoveAppsService> _logger;
    private readonly Lock _lock = new();
    private HashSet<RemovableApp> _lastList = [];

    public RemoveAppsService(
        IInstalledAppsReader reader,
        IWingetClient winget,
        IAppUninstaller uninstaller,
        ILogger<RemoveAppsService> logger)
    {
        _reader = reader;
        _winget = winget;
        _uninstaller = uninstaller;
        _logger = logger;
    }

    public event EventHandler<AppRemovedEventArgs>? AppRemoved;

    public async Task<IReadOnlyList<RemovableApp>> ListAsync(CancellationToken cancellationToken)
    {
        var apps = await Task.Run(_reader.GetAllInstalledApps, cancellationToken).ConfigureAwait(false);
        var wingetPackages = await ReadWingetPackagesAsync(cancellationToken).ConfigureAwait(false);

        var list = new List<RemovableApp>();
        foreach (var app in apps)
        {
            var kind = AppProtectionRules.Classify(app);
            if (kind == RemovableAppKind.Porchlight)
            {
                continue;
            }

            var normal = kind == RemovableAppKind.Normal;
            var id = kind is RemovableAppKind.Normal or RemovableAppKind.SystemPart
                ? WingetIdMapper.FindId(app, wingetPackages)
                : null;
            list.Add(new RemovableApp(app, kind, normal && OftenPreinstalledCatalog.IsOftenPreinstalled(app.DisplayName), id));
        }

        lock (_lock)
        {
            _lastList = [.. list];
        }

        return list;
    }

    public async Task<RemoveAppOutcome> RemoveAsync(RemovableApp app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        bool known;
        lock (_lock)
        {
            known = _lastList.Contains(app);
        }

        if (!app.CanRemove || !known)
        {
            LogRefused(app.App.DisplayName);
            return new RemoveAppOutcome(RemoveAppResult.Refused);
        }

        if (!_uninstaller.CanStartUninstall(app.App))
        {
            return new RemoveAppOutcome(RemoveAppResult.BlockedWhileElevated);
        }

        if (app.WingetId is { } id)
        {
            var viaWinget = await TryWingetAsync(app, id).ConfigureAwait(false);
            if (viaWinget is not null)
            {
                return viaWinget;
            }
        }

        return StartOwnUninstaller(app);
    }

    /// <summary>Returns null when winget is not installed, so the caller can fall back.</summary>
    private async Task<RemoveAppOutcome?> TryWingetAsync(RemovableApp app, string id)
    {
        WingetResult result;
        try
        {
            // Never cancelled once started: killing winget mid-uninstall can leave a half-removed app.
            result = await _winget.UninstallAsync(id, silent: true, log: null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WingetNotFoundException)
        {
            LogWingetMissing(app.App.DisplayName);
            return null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            LogWingetFailed(ex, app.App.DisplayName);
            return new RemoveAppOutcome(RemoveAppResult.Failed);
        }

        if (result.ExitCode == 0)
        {
            AppRemoved?.Invoke(this, new AppRemovedEventArgs(app));
            return new RemoveAppOutcome(RemoveAppResult.Removed);
        }

        var outcome = WingetExitCodes.DescribeOutcome(result.ExitCode, result.Lines);
        LogWingetExit(app.App.DisplayName, outcome.ExitCodeHex);
        return new RemoveAppOutcome(RemoveAppResult.WingetProblem, outcome);
    }

    private RemoveAppOutcome StartOwnUninstaller(RemovableApp app) =>
        _uninstaller.StartUninstall(app.App) switch
        {
            UninstallStartResult.Started => new RemoveAppOutcome(RemoveAppResult.UninstallerOpened),
            UninstallStartResult.BlockedWhileElevated => new RemoveAppOutcome(RemoveAppResult.BlockedWhileElevated),
            UninstallStartResult.InvalidCommand => new RemoveAppOutcome(RemoveAppResult.InvalidCommand),
            _ => new RemoveAppOutcome(RemoveAppResult.Failed),
        };

    private async Task<IReadOnlyList<WingetInstalledPackage>> ReadWingetPackagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _winget.ListInstalledAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (WingetNotFoundException)
        {
            LogWingetMissingForList();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            LogWingetListFailed(ex);
        }

        return [];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused to remove {App}: not removable or not in the last list.")]
    private partial void LogRefused(string app);

    [LoggerMessage(Level = LogLevel.Information, Message = "winget is not installed; using the own uninstaller for {App}.")]
    private partial void LogWingetMissing(string app);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Running winget to remove {App} failed.")]
    private partial void LogWingetFailed(Exception ex, string app);

    [LoggerMessage(Level = LogLevel.Information, Message = "winget did not remove {App} (exit code {Code}).")]
    private partial void LogWingetExit(string app, string code);

    [LoggerMessage(Level = LogLevel.Debug, Message = "winget is not installed; no winget ids for the app list.")]
    private partial void LogWingetMissingForList();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading winget's installed list failed; using the apps' own uninstallers.")]
    private partial void LogWingetListFailed(Exception ex);
}
