using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Health;

/// <inheritdoc cref="IWindowsRepairService"/>
public sealed partial class WindowsRepairService(IProcessRunner processRunner, ILogger<WindowsRepairService> logger)
    : IWindowsRepairService
{
    public async Task<SfcOutcome> RunSfcAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var collected = new List<string>();
        try
        {
            // CancellationToken.None on purpose: cancelling kills the process tree, which is unsafe
            // for SFC once it is repairing files. See IProcessRunner.RunAsync docs.
            await RunToolAsync("sfc.exe", ["/scannow"], SfcOutputParser.TryParseProgress, collected, progress)
                .ConfigureAwait(false);
            return SfcOutputParser.Parse(collected);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            LogToolFailed(ex, "sfc");
            return SfcOutcome.CouldNotRun;
        }
    }

    public async Task<DismOutcome> RunDismAsync(IProgress<RepairProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var collected = new List<string>();
        try
        {
            var exitCode = await RunToolAsync(
                "dism.exe", ["/Online", "/Cleanup-Image", "/RestoreHealth"],
                DismOutputParser.TryParseProgress, collected, progress).ConfigureAwait(false);
            return DismOutputParser.Parse(exitCode, collected);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            LogToolFailed(ex, "dism");
            return DismOutcome.Failed;
        }
    }

    private async Task<int> RunToolAsync(
        string exeName,
        string[] arguments,
        TryParsePercent tryParsePercent,
        List<string> collected,
        IProgress<RepairProgress>? progress)
    {
        var exePath = Path.Combine(Environment.SystemDirectory, exeName);

        // sfc.exe's UTF-16 output decoded as UTF-8 makes a "\r\n" ending look like a lone "\r" redraw
        // (see RepairOutputCleaner), so real text can arrive through EITHER callback: treat both alike.
        var handler = new LineHandler(collected, tryParsePercent, progress);
        var result = await processRunner.RunAsync(
            exePath, arguments, handler, handler, CancellationToken.None).ConfigureAwait(false);

        // Anything the callbacks did not see (defensive; the runner also returns all complete lines).
        foreach (var line in result.StandardOutputLines.Concat(result.StandardErrorLines))
        {
            var cleaned = RepairOutputCleaner.Clean(line);
            if (cleaned.Length > 0 && !collected.Contains(cleaned))
            {
                collected.Add(cleaned);
            }
        }

        return result.ExitCode;
    }

    private delegate bool TryParsePercent(string? line, out int percent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not run {Tool}.")]
    private partial void LogToolFailed(Exception ex, string tool);

    private sealed class LineHandler(
        List<string> collected, TryParsePercent tryParsePercent, IProgress<RepairProgress>? progress)
        : IProgress<string>
    {
        private readonly Lock _lock = new();

        public void Report(string value)
        {
            var cleaned = RepairOutputCleaner.Clean(value);
            if (cleaned.Length == 0)
            {
                return;
            }

            var isProgress = tryParsePercent(cleaned, out var percent);
            lock (_lock)
            {
                collected.Add(cleaned);
            }

            progress?.Report(isProgress
                ? new RepairProgress(percent, null)
                : new RepairProgress(null, cleaned));
        }
    }
}
