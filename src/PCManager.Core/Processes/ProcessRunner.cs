using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PCManager.Core.Processes;

/// <inheritdoc cref="IProcessRunner"/>
public sealed partial class ProcessRunner : IProcessRunner
{
    private const int BufferSize = 4096;

    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IProgress<string>? onLine,
        IProgress<string>? onProgress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Some CLI tools (e.g. AnyDesk's --get-id) block waiting for stdin if it looks like it
            // might still receive input. We never send any, so redirect it and close it immediately
            // below - that gives the child process EOF instead of an inherited (and, with
            // CreateNoWindow, nonexistent) console handle it could hang on.
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        // Process.Start() does synchronous work (creating the child process); running it via
        // Task.Run keeps a caller who awaits this method without switching threads first (e.g. a
        // view model still on the UI thread) from blocking the dispatcher on it.
        var started = await Task.Run(() => process.Start(), cancellationToken).ConfigureAwait(false);
        if (!started)
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        process.StandardInput.Close();

        // Killing on cancellation (rather than only stopping our own reads) means a cancelled
        // install/start does not leave winget (or whatever we launched) running in the background.
        // IMPORTANT: for an installer that must not be interrupted mid-write (e.g. winget install,
        // which can leave a driver half-installed), callers pass CancellationToken.None here once
        // the process has actually started - see IComponentService.InstallAsync. Cancelling this
        // token kills the whole process tree; it is not a graceful "let it finish" cancellation.
        await using var registration = cancellationToken.Register(() => TryKill(process));

        var outputReader = new WingetOutputReader(onLine, onProgress);
        var errorReader = new WingetOutputReader(onLine);
        var stdoutTask = PumpAsync(process.StandardOutput, outputReader, cancellationToken);
        var stderrTask = PumpAsync(process.StandardError, errorReader, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        outputReader.Complete();
        errorReader.Complete();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessRunResult(process.ExitCode, outputReader.Lines, errorReader.Lines);
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
    }

    /// <summary>Reads a stream to completion, feeding every chunk to <paramref name="reader"/>
    /// (shared by stdout and stderr, each with their own <see cref="WingetOutputReader"/> instance
    /// so their lines are not interleaved character-by-character).</summary>
    private static async Task PumpAsync(
        StreamReader stream, WingetOutputReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            reader.Feed(buffer.AsSpan(0, read));
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            // The process exited between the HasExited check and Kill(); nothing left to do.
            LogKillRace(ex);
        }
        catch (Win32Exception ex)
        {
            // The OS refused the kill (e.g. the process is already exiting, or access is denied
            // for a child that reparented under a different user). Cancellation must never throw
            // out of the registration callback, so this is logged, not rethrown.
            LogKillFailed(ex);
        }
        catch (AggregateException ex)
        {
            // Kill(entireProcessTree: true) aggregates per-child failures; same reasoning as above.
            LogKillFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process already exited before it could be killed on cancellation.")]
    private partial void LogKillRace(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not kill process tree on cancellation.")]
    private partial void LogKillFailed(Exception exception);
}
