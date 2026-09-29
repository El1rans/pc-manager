using System.ComponentModel;
using System.Diagnostics;

namespace Porchlight.Core.Elevation;

/// <inheritdoc cref="IElevatedCommandRunner"/>
public sealed class ElevatedCommandRunner : IElevatedCommandRunner
{
    /// <summary>Win32 error code for "the operation was canceled by the user" (UAC decline).</summary>
    private const int ErrorCancelled = 1223;

    public Task<ElevatedRunResult> RunAsync(
        string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        // WaitForExit blocks, so the whole thing runs on a pool thread.
        return Task.Run(
            () =>
            {
                var startInfo = new ProcessStartInfo(fileName)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                foreach (var arg in arguments)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                try
                {
                    using var process = Process.Start(startInfo)
                        ?? throw new InvalidOperationException("The elevated process did not start.");
                    process.WaitForExit();
                    return new ElevatedRunResult(Declined: false, process.ExitCode);
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
                {
                    return new ElevatedRunResult(Declined: true, ExitCode: -1);
                }
            },
            cancellationToken);
    }
}
