using System.ComponentModel;
using Microsoft.Extensions.Logging;
using PCManager.Core.Processes;

namespace PCManager.Core.Winget;

/// <inheritdoc cref="IWingetClient"/>
public sealed partial class WingetClient : IWingetClient
{
    private const string Executable = "winget";

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

    private async Task<ProcessRunResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _processRunner.RunAsync(Executable, arguments, onLine, onProgress, cancellationToken)
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
