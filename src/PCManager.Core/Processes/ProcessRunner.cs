using System.Diagnostics;
using System.Text;

namespace PCManager.Core.Processes;

/// <inheritdoc cref="IProcessRunner"/>
public sealed class ProcessRunner : IProcessRunner
{
    private const int BufferSize = 4096;

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
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        // Killing on cancellation (rather than only stopping our own reads) means a cancelled
        // install/start does not leave winget (or whatever we launched) running in the background.
        await using var registration = cancellationToken.Register(() => TryKill(process));

        var reader = new WingetOutputReader(onLine, onProgress);
        var stdoutTask = PumpStandardOutputAsync(process.StandardOutput, reader, cancellationToken);
        var stderrTask = PumpStandardErrorAsync(process.StandardError, onLine, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        reader.Complete();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessRunResult(process.ExitCode, reader.Lines);
    }

    private static async Task PumpStandardOutputAsync(
        StreamReader standardOutput, WingetOutputReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[BufferSize];
        int read;
        while ((read = await standardOutput.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            reader.Feed(buffer.AsSpan(0, read));
        }
    }

    /// <summary>
    /// Read concurrently with stdout (not after it) so a process that writes enough to either
    /// stream to fill its OS pipe buffer cannot deadlock waiting for us to drain the other one.
    /// </summary>
    private static async Task PumpStandardErrorAsync(
        StreamReader standardError, IProgress<string>? onLine, CancellationToken cancellationToken)
    {
        var text = await standardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var trimmed = text.Trim();
        if (trimmed.Length > 0)
        {
            onLine?.Report(trimmed);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the HasExited check and Kill(); nothing left to do.
        }
    }
}
