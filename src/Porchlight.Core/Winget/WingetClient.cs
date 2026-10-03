using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Winget;

/// <inheritdoc cref="IWingetClient"/>
public sealed partial class WingetClient : IWingetClient
{
    /// <summary>Most results <c>winget search</c> is asked for (the Get apps page's list cap).</summary>
    private const int SearchResultLimit = 50;

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<WingetClient> _logger;

    public WingetClient(IProcessRunner processRunner, ILogger<WingetClient> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WingetPackage>> GetUpgradesAsync(
        bool includeUnknown, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        List<string> arguments = ["upgrade", "--accept-source-agreements", "--disable-interactivity"];
        if (includeUnknown)
        {
            arguments.Add("--include-unknown");
        }

        var result = await RunAsync(arguments, onLine: null, progress, cancellationToken).ConfigureAwait(false);
        return WingetTableParser.Parse(result.StandardOutputLines, _logger);
    }

    public async Task<WingetResult> UpgradeAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        List<string> arguments =
        [
            "upgrade",
            "--id", id,
            "--exact",
            "--include-unknown",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity",
        ];
        if (silent)
        {
            arguments.Add("--silent");
        }

        var result = await RunAsync(arguments, log, progress, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    public async Task<WingetResult> ShowAsync(string id, IProgress<string>? log, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        string[] arguments = ["show", "--id", id, "--exact", "--accept-source-agreements", "--disable-interactivity"];
        var result = await RunAsync(arguments, log, onProgress: null, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    public async Task<WingetResult> UninstallAsync(
        string id, bool silent, IProgress<string>? log, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        List<string> arguments = ["uninstall", "--id", id, "--exact", "--disable-interactivity"];
        if (silent)
        {
            arguments.Add("--silent");
        }

        var result = await RunAsync(arguments, log, onProgress: null, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    public async Task<WingetResult> InstallAsync(
        string id, bool silent, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        List<string> arguments =
        [
            "install",
            "--id", id,
            "--exact",
            "--source", "winget",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity",
        ];
        if (silent)
        {
            arguments.Add("--silent");
        }

        var result = await RunAsync(arguments, log, progress, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    public async Task<IReadOnlyList<WingetSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        string[] arguments =
        [
            "search",
            "--query", query,
            "--source", "winget",
            "--count", SearchResultLimit.ToString(CultureInfo.InvariantCulture),
            "--accept-source-agreements",
            "--disable-interactivity",
        ];
        var result = await RunAsync(arguments, onLine: null, onProgress: null, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode is not 0 and not WingetExitCodes.NoApplicationsFound)
        {
            throw new InvalidOperationException(
                $"winget search failed with exit code 0x{unchecked((uint)result.ExitCode):X8}.");
        }

        return WingetSearchTableParser.Parse(result.StandardOutputLines, _logger);
    }

    public async Task<IReadOnlySet<string>> ListInstalledIdsAsync(CancellationToken cancellationToken)
    {
        string[] arguments = ["list", "--source", "winget", "--accept-source-agreements", "--disable-interactivity"];
        var result = await RunAsync(arguments, onLine: null, onProgress: null, cancellationToken).ConfigureAwait(false);
        return WingetInstalledIdsParser.Parse(result.StandardOutputLines, _logger);
    }

    public async Task<IReadOnlyList<WingetInstalledPackage>> ListInstalledAsync(CancellationToken cancellationToken)
    {
        string[] arguments = ["list", "--source", "winget", "--accept-source-agreements", "--disable-interactivity"];
        var result = await RunAsync(arguments, onLine: null, onProgress: null, cancellationToken).ConfigureAwait(false);
        return WingetInstalledListParser.Parse(result.StandardOutputLines, _logger);
    }

    public async Task<WingetResult> ExportAsync(
        string filePath, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        string[] arguments = ["export", "-o", filePath, "--accept-source-agreements", "--disable-interactivity"];
        var result = await RunAsync(arguments, log, progress, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    public async Task<WingetResult> ImportAsync(
        string filePath, IProgress<string>? log, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        string[] arguments =
        [
            "import",
            "-i", filePath,
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--ignore-unavailable",
            "--disable-interactivity",
        ];
        var result = await RunAsync(arguments, log, progress, cancellationToken).ConfigureAwait(false);
        return new WingetResult(result.ExitCode, result.StandardOutputLines);
    }

    private async Task<ProcessRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        // Logs the exact command line (every argument, not just --id) before running it - a
        // maintainer diagnosing a real failure needs to see --silent/--include-unknown/etc. exactly
        // as passed, not a hand-written approximation. No secrets ever appear here (package ids and
        // winget's own fixed flags only). Only reported when the caller actually wants a log (a
        // plain listing check passes onLine: null and has no per-run log entry of its own).
        var executable = WingetLocator.Resolve();
        onLine?.Report("> winget " + string.Join(' ', arguments));

        try
        {
            return await _processRunner.RunAsync(executable, arguments, onLine, onProgress, cancellationToken)
                .ConfigureAwait(false);
        }
        // NativeErrorCode 2 is ERROR_FILE_NOT_FOUND - winget.exe is not on PATH. Any other Win32
        // error (e.g. access denied) is a different, unexpected problem and must not be
        // misreported as "winget is not installed".
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            LogWingetNotFound(ex);
            throw new WingetNotFoundException(
                "winget.exe was not found. Install \"App Installer\" from the Microsoft Store.", ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start winget.exe.")]
    private partial void LogWingetNotFound(Exception exception);
}
