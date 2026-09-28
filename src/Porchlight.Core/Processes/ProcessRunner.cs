using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Porchlight.Core.Processes;

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

        // Neither of the awaits above reliably observes a cancellation that fires mid-run. Killing
        // the tree closes the pipes, so a read the token failed to interrupt (the stdout/stderr
        // pipes are synchronous handles, and interrupting a blocked read is best-effort) simply
        // returns EOF, and WaitForExitAsync returns normally - even with a cancelled token - once
        // the process has already exited. Without this check a killed process would be reported
        // as a normal run with exit code -1 instead of the OperationCanceledException the
        // IProcessRunner contract promises.
        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessRunResult(process.ExitCode, outputReader.Lines, errorReader.Lines);
    }

    public void StartDetached(string fileName, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = BuildDetachedStartInfo(fileName, arguments);

        // Deliberately not disposed and not captured in a `using`: this is a fire-and-forget
        // launch of a process meant to keep running long after this call returns (e.g. OpenRGB's
        // SDK server - see IProcessRunner.StartDetached's doc comment). Process.Dispose() itself
        // does not kill the child on .NET, but there is no reason to hold or release a handle to a
        // process this class has no further business with - see the regression this guards
        // against: docs/specs/05-lighting.md addendum, "OpenRGB auto-start reliability".
        Process.Start(startInfo);
    }

    /// <summary>
    /// Builds the <see cref="ProcessStartInfo"/> for a truly detached, independent launch:
    /// <see cref="ProcessStartInfo.UseShellExecute"/> true (so the OS shell, not Porchlight, is the
    /// process' creator - it is never added to any job object Porchlight's own process belongs to,
    /// so it cannot be torn down if that job is ever closed) and no stdio redirection at all (a
    /// redirected pipe closing - e.g. if a wrapper ever disposed the returned <see cref="Process"/>
    /// - can itself make some apps exit; see the regression this fixes below). Internal so a test
    /// can assert these without actually spawning a process.
    /// </summary>
    /// <remarks>
    /// Regression: an earlier version used <c>UseShellExecute = false</c> with
    /// <c>using var process = Process.Start(startInfo);</c>. On the maintainer's PC, OpenRGB
    /// (started this way with <c>--server --startminimized</c>) reliably exited 5-10 seconds after
    /// launch, while the identical command run manually from a shell stayed up indefinitely -
    /// i.e. something about *how* Porchlight launched it, not the command itself, ended it early.
    /// Launching through the shell instead removes Porchlight as the direct process creator/owner
    /// entirely, which is the standard fix for "fire-and-forget, must outlive the launcher" process
    /// starts on Windows.
    /// </remarks>
    internal static ProcessStartInfo BuildDetachedStartInfo(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
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
