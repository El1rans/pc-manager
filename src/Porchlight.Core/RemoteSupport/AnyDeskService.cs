using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;

namespace Porchlight.Core.RemoteSupport;

/// <inheritdoc cref="IAnyDeskService"/>
public sealed partial class AnyDeskService : IAnyDeskService
{
    /// <summary>How long a single <c>--get-id</c>/<c>--get-alias</c> call is given before it is
    /// treated as failed and the config-file fallback is used instead.</summary>
    private static readonly TimeSpan DefaultCliCallTimeout = TimeSpan.FromSeconds(5);

    // AnyDesk numeric IDs are 9-10 digits; a licensed alias looks like "name@ad" (an
    // alphanumeric/dot/dash/underscore name, an "@", then the same for the namespace). Anything
    // else from the CLI or a config file is untrusted output (garbage, a truncated line, a stray
    // ANSI-wrapped prompt) and is treated as "unknown" rather than shown to the user or copied.
    [GeneratedRegex(@"^\d{9,10}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^[\w.-]+@[\w.-]+$")]
    private static partial Regex AliasPattern();

    // Strips ANSI/VT escape sequences (e.g. a color-coded CLI prompt) and other control characters
    // that a terminal would interpret but that have no business in an ID/alias value.
    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlAndAnsiSequences();

    private readonly IComponentService _componentService;
    private readonly IProcessRunner _processRunner;
    private readonly IAnyDeskConfigReader _configReader;
    private readonly IElevationService _elevationService;
    private readonly ILogger<AnyDeskService> _logger;
    private readonly TimeSpan _cliCallTimeout;

    public AnyDeskService(
        IComponentService componentService,
        IProcessRunner processRunner,
        IAnyDeskConfigReader configReader,
        IElevationService elevationService,
        ILogger<AnyDeskService> logger)
        : this(componentService, processRunner, configReader, elevationService, logger, DefaultCliCallTimeout)
    {
    }

    /// <summary>Test seam: lets tests use a much shorter CLI timeout than production.</summary>
    public AnyDeskService(
        IComponentService componentService,
        IProcessRunner processRunner,
        IAnyDeskConfigReader configReader,
        IElevationService elevationService,
        ILogger<AnyDeskService> logger,
        TimeSpan cliCallTimeout)
    {
        _componentService = componentService;
        _processRunner = processRunner;
        _configReader = configReader;
        _elevationService = elevationService;
        _logger = logger;
        _cliCallTimeout = cliCallTimeout;
    }

    public async Task<AnyDeskState> GetStateAsync(CancellationToken cancellationToken)
    {
        var status = await _componentService.GetStatusAsync(ComponentIds.AnyDesk, cancellationToken)
            .ConfigureAwait(false);
        return await BuildStateAsync(status, cancellationToken).ConfigureAwait(false);
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

        // Never execute a user-writable AnyDesk.exe (an HKCU-registered install, or - not
        // applicable to this component today, but the same rule OpenRGB's path override needs -
        // any other non-admin-only-writable location) while Porchlight itself is elevated: a
        // planted binary there would then run with administrator rights just from opening this
        // page. IComponentService.StartAsync already enforces this for actually launching AnyDesk;
        // this covers the separate --get-id/--get-alias reads this service makes directly.
        var skipCli = _elevationService.IsElevated && !status.PathIsTrusted;
        if (skipCli)
        {
            LogSkippingCliUntrustedElevated(status.Path);
        }

        var (id, alias) = await ReadIdAndAliasAsync(status.Path, skipCli, cancellationToken).ConfigureAwait(false);
        return new AnyDeskState(true, status.Path, status.Version, id, alias, isRunning, status);
    }

    private async Task<(string? Id, string? Alias)> ReadIdAndAliasAsync(
        string exePath, bool skipCli, CancellationToken cancellationToken)
    {
        string? id = null;
        string? alias = null;

        if (!skipCli)
        {
            var (idValue, idTimedOut) = await RunGetAsync(exePath, "--get-id", cancellationToken).ConfigureAwait(false);
            id = ValidateId(idValue);

            // If reading the ID itself already timed out, skip the alias call too rather than
            // paying the full timeout twice - the config-file fallback below covers both anyway.
            if (!idTimedOut)
            {
                var (aliasValue, _) = await RunGetAsync(exePath, "--get-alias", cancellationToken).ConfigureAwait(false);
                alias = ValidateAlias(aliasValue);
            }
        }

        if (id is not null)
        {
            return (id, alias);
        }

        // Try system.conf first; only fall back to service.conf if system.conf itself has no ID
        // line (rather than just because it is missing/unreadable) - both are checked, since an ID
        // can be present in either depending on the installed AnyDesk version.
        var (systemConfId, systemConfAlias) = ParseConfigFile(_configReader.SystemConfPath);
        if (systemConfId is not null)
        {
            return (systemConfId, alias ?? systemConfAlias);
        }

        var (serviceConfId, serviceConfAlias) = ParseConfigFile(_configReader.ServiceConfPath);
        return (serviceConfId, alias ?? systemConfAlias ?? serviceConfAlias);
    }

    private (string? Id, string? Alias) ParseConfigFile(string path)
    {
        var text = _configReader.TryRead(path);
        if (text is null)
        {
            return (null, null);
        }

        var (id, alias) = AnyDeskConfigParser.Parse(text);
        return (ValidateId(id), ValidateAlias(alias));
    }

    private static string? ValidateId(string? value) =>
        value is not null && IdPattern().IsMatch(value) ? value : null;

    private static string? ValidateAlias(string? value) =>
        value is not null && AliasPattern().IsMatch(value) ? value : null;

    /// <returns>The first non-blank output line (sanitized of control/ANSI characters), and whether
    /// the call was abandoned because it ran past <see cref="_cliCallTimeout"/> (as opposed to
    /// exiting with a non-zero code or producing no output) - see the alias-skipping logic above.</returns>
    private async Task<(string? Value, bool TimedOut)> RunGetAsync(
        string exePath, string argument, CancellationToken cancellationToken)
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
                return (null, false);
            }

            var value = FirstNonBlankSanitizedLine(result.StandardOutputLines);
            return (value, false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own CliCallTimeout fired, not the caller's token - this is an expected "the CLI
            // call took too long" outcome, not a genuine cancellation; fall back to the config file.
            LogCliCallTimedOut(argument);
            return (null, true);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not run AnyDesk.exe {Argument} at {ExePath}.", argument, exePath);
            return (null, false);
        }
    }

    private static string? FirstNonBlankSanitizedLine(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var sanitized = ControlAndAnsiSequences().Replace(line, string.Empty).Trim();
            if (sanitized.Length > 0)
            {
                return sanitized;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "AnyDesk.exe {Argument} exited with code {ExitCode}; falling back to the config file.")]
    private partial void LogCliCallFailed(string argument, int exitCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AnyDesk.exe {Argument} timed out; falling back to the config file.")]
    private partial void LogCliCallTimedOut(string argument);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping AnyDesk CLI reads at {Path}: Porchlight is elevated and this path is not admin-only-writable.")]
    private partial void LogSkippingCliUntrustedElevated(string path);
}
