using Microsoft.Extensions.Logging;
using PCManager.Core.Components;
using PCManager.Core.Processes;

namespace PCManager.Core.RemoteSupport;

/// <inheritdoc cref="IAnyDeskService"/>
public sealed partial class AnyDeskService : IAnyDeskService
{
    /// <summary>How long a single <c>--get-id</c>/<c>--get-alias</c> call is given before it is
    /// treated as failed and the config-file fallback is used instead.</summary>
    private static readonly TimeSpan DefaultCliCallTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long <see cref="InstallAsync"/> polls for an ID to appear after install, before
    /// giving up and returning whatever state it last saw.</summary>
    private static readonly TimeSpan DefaultInstallIdPollTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan DefaultInstallIdPollInterval = TimeSpan.FromSeconds(2);

    private readonly IComponentService _componentService;
    private readonly IProcessRunner _processRunner;
    private readonly IAnyDeskConfigReader _configReader;
    private readonly ILogger<AnyDeskService> _logger;
    private readonly TimeSpan _cliCallTimeout;
    private readonly TimeSpan _installIdPollTimeout;
    private readonly TimeSpan _installIdPollInterval;

    public AnyDeskService(
        IComponentService componentService,
        IProcessRunner processRunner,
        IAnyDeskConfigReader configReader,
        ILogger<AnyDeskService> logger)
        : this(
            componentService, processRunner, configReader, logger,
            DefaultCliCallTimeout, DefaultInstallIdPollTimeout, DefaultInstallIdPollInterval)
    {
    }

    /// <summary>Test seam: lets tests use much shorter timeouts/intervals than production so a
    /// polling test does not take tens of seconds to run.</summary>
    public AnyDeskService(
        IComponentService componentService,
        IProcessRunner processRunner,
        IAnyDeskConfigReader configReader,
        ILogger<AnyDeskService> logger,
        TimeSpan cliCallTimeout,
        TimeSpan installIdPollTimeout,
        TimeSpan installIdPollInterval)
    {
        _componentService = componentService;
        _processRunner = processRunner;
        _configReader = configReader;
        _logger = logger;
        _cliCallTimeout = cliCallTimeout;
        _installIdPollTimeout = installIdPollTimeout;
        _installIdPollInterval = installIdPollInterval;
    }

    public async Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken)
    {
        var status = await _componentService.GetStatusAsync(ComponentIds.AnyDesk, cancellationToken)
            .ConfigureAwait(false);
        return await BuildStateAsync(status, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AnyDeskState> InstallAsync(IProgress<string> log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);

        // No separate progress-bar text for this feature's install (just the plain-language log);
        // IComponentService.InstallAsync still requires an IProgress<string>, so pass a no-op.
        var noOpProgress = new Progress<string>();
        var status = await _componentService
            .InstallAsync(ComponentIds.AnyDesk, log, noOpProgress, cancellationToken)
            .ConfigureAwait(false);

        var state = await BuildStateAsync(status, CancellationToken.None).ConfigureAwait(false);
        if (state.IsError || !state.IsInstalled)
        {
            return state;
        }

        // AnyDesk only generates its ID on first start, so an install that has never run it yet
        // reports Installed with no ID. Poll for up to InstallIdPollTimeout, starting it once along
        // the way, so the caller does not have to restart PC Manager to see the address.
        var deadline = DateTime.UtcNow + _installIdPollTimeout;
        var hasStarted = state.IsRunning;
        while (state.Id is null && DateTime.UtcNow < deadline)
        {
            if (!hasStarted)
            {
                await _componentService.StartAsync(ComponentIds.AnyDesk, CancellationToken.None)
                    .ConfigureAwait(false);
                hasStarted = true;
            }

            await Task.Delay(_installIdPollInterval, CancellationToken.None).ConfigureAwait(false);

            var refreshedStatus = await _componentService
                .GetStatusAsync(ComponentIds.AnyDesk, CancellationToken.None)
                .ConfigureAwait(false);
            state = await BuildStateAsync(refreshedStatus, CancellationToken.None).ConfigureAwait(false);
        }

        return state;
    }

    public async Task<AnyDeskState> LaunchAsync(CancellationToken cancellationToken)
    {
        var status = await _componentService.StartAsync(ComponentIds.AnyDesk, cancellationToken)
            .ConfigureAwait(false);
        return await BuildStateAsync(status, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AnyDeskState> BuildStateAsync(ComponentStatus status, CancellationToken cancellationToken)
    {
        var isInstalled = status.State is ComponentState.Installed or ComponentState.Running;
        var isRunning = status.State == ComponentState.Running;

        if (!isInstalled || status.Path is null)
        {
            return new AnyDeskState(isInstalled, status.Path, status.Version, null, null, isRunning, status);
        }

        var (id, alias) = await ReadIdAndAliasAsync(status.Path, cancellationToken).ConfigureAwait(false);
        return new AnyDeskState(true, status.Path, status.Version, id, alias, isRunning, status);
    }

    private async Task<(string? Id, string? Alias)> ReadIdAndAliasAsync(string exePath, CancellationToken cancellationToken)
    {
        var id = await RunGetAsync(exePath, "--get-id", cancellationToken).ConfigureAwait(false);
        var alias = await RunGetAsync(exePath, "--get-alias", cancellationToken).ConfigureAwait(false);

        if (id is null)
        {
            // Try system.conf first; only fall back to service.conf if system.conf itself has no
            // ID line (rather than just because it is missing/unreadable) - both are checked, since
            // an ID can be present in either depending on the installed AnyDesk version.
            var (systemConfId, systemConfAlias) = ParseConfigFile(_configReader.SystemConfPath);
            if (systemConfId is not null)
            {
                id = systemConfId;
                alias ??= systemConfAlias;
            }
            else
            {
                var (serviceConfId, serviceConfAlias) = ParseConfigFile(_configReader.ServiceConfPath);
                id ??= serviceConfId;
                alias ??= systemConfAlias ?? serviceConfAlias;
            }
        }

        return (id, alias);
    }

    private (string? Id, string? Alias) ParseConfigFile(string path)
    {
        var text = _configReader.TryRead(path);
        return text is null ? (null, null) : AnyDeskConfigParser.Parse(text);
    }

    private async Task<string?> RunGetAsync(string exePath, string argument, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_cliCallTimeout);

        try
        {
            var result = await _processRunner
                .RunAsync(exePath, [argument], onLine: null, onProgress: null, timeoutCts.Token)
                .ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                LogCliCallFailed(argument, result.ExitCode);
                return null;
            }

            var value = result.StandardOutputLines.Count > 0 ? result.StandardOutputLines[0].Trim() : null;
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own CliCallTimeout fired, not the caller's token - this is an expected "the CLI
            // call took too long" outcome, not a genuine cancellation; fall back to the config file.
            LogCliCallTimedOut(argument);
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not run AnyDesk.exe {Argument} at {ExePath}.", argument, exePath);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "AnyDesk.exe {Argument} exited with code {ExitCode}; falling back to the config file.")]
    private partial void LogCliCallFailed(string argument, int exitCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AnyDesk.exe {Argument} timed out; falling back to the config file.")]
    private partial void LogCliCallTimedOut(string argument);
}
