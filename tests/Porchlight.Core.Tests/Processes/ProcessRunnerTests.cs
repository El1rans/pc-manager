using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.Core.Tests.Processes;

public sealed class ProcessRunnerTests
{
    private static ProcessRunner CreateRunner() => new(NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task RunAsync_EchoesOutput_ReturnsExitCodeAndLines()
    {
        var runner = CreateRunner();
        var lines = new List<string>();

        var result = await runner.RunAsync(
            "cmd.exe",
            ["/c", "echo hello&echo world"],
            new RecordingProgress(lines),
            onProgress: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["hello", "world"], result.StandardOutputLines);
        Assert.Equal(["hello", "world"], lines);
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_ReturnsThatExitCode()
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(
            "cmd.exe", ["/c", "exit 7"], onLine: null, onProgress: null, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_Cancelled_ThrowsAndKillsProcessTree()
    {
        var runner = CreateRunner();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pidsReported = new TaskCompletionSource<(int Child, int Grandchild)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // PowerShell starts PING.EXE as its own child, then reports both PIDs on stdout, so the
        // test cancels only once the whole tree is known to be running (no wall-clock race) and
        // checks exactly those two processes rather than every PING on the machine.
        var runTask = runner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", ReportPidsThenWaitScript],
            new CallbackProgress(line =>
            {
                if (TryParsePids(line, out var pids))
                {
                    pidsReported.TrySetResult(pids);
                }
            }),
            onProgress: null,
            cts.Token);

        var (childPid, grandchildPid) = await pidsReported.Task
            .WaitAsync(StartupTimeout, TestContext.Current.CancellationToken);

        // Open handles now, while both are known to be alive, so the exit checks below track these
        // exact processes even if the OS later reuses their PIDs.
        using var child = OpenTracked(childPid);
        using var grandchild = OpenTracked(grandchildPid);
        try
        {
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

            await child.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(ExitTimeout, TestContext.Current.CancellationToken);
            await grandchild.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(ExitTimeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Only reached with live processes if an assertion above failed; never leave the
            // processes this test started running.
            KillIfRunning(child);
            KillIfRunning(grandchild);
        }
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelled_Throws()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            "cmd.exe", ["/c", "exit 0"], onLine: null, onProgress: null, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task RunAsync_StderrOutput_IsReportedToOnLineAndReturnedSeparately()
    {
        var runner = CreateRunner();
        var lines = new List<string>();

        var result = await runner.RunAsync(
            "cmd.exe",
            ["/c", "echo oops 1>&2"],
            new RecordingProgress(lines),
            onProgress: null,
            TestContext.Current.CancellationToken);

        Assert.Contains(lines, l => l.Contains("oops", StringComparison.Ordinal));
        Assert.Contains(result.StandardErrorLines, l => l.Contains("oops", StringComparison.Ordinal));
        Assert.DoesNotContain(result.StandardOutputLines, l => l.Contains("oops", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ExecutableDoesNotExist_ThrowsWin32Exception()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAsync<Win32Exception>(() => runner.RunAsync(
            "porchlight-definitely-not-a-real-executable-12345.exe",
            [],
            onLine: null,
            onProgress: null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BuildDetachedStartInfo_UsesShellExecuteAndNoRedirection()
    {
        // Regression for a real bug: an earlier version used UseShellExecute=false plus
        // `using var process = Process.Start(...)`, and OpenRGB (started with
        // "--server --startminimized" - see ComponentCatalog) reliably exited 5-10 seconds after
        // launch, while running the identical command manually from a shell stayed up
        // indefinitely. Launching through the shell removes Porchlight as the child's direct
        // process creator/owner (it is never added to any job Porchlight belongs to, and there are
        // no stdio pipes to it that could be closed out from under it) - see docs/specs/05-lighting
        // .md addendum, "OpenRGB auto-start reliability".
        var startInfo = ProcessRunner.BuildDetachedStartInfo("openrgb.exe", ["--server", "--startminimized"]);

        Assert.True(startInfo.UseShellExecute);
        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.RedirectStandardInput);
        Assert.Equal(["--server", "--startminimized"], startInfo.ArgumentList);
        Assert.Equal("openrgb.exe", startInfo.FileName);
    }

    // Deliberately no live-process end-to-end test for StartDetached here (e.g. "spawn cmd.exe and
    // check it's still alive after N seconds"): many CI/build-agent runners (and this repo's own
    // sandboxed dev environment) wrap the whole `dotnet test` process tree in a job object that
    // kills descendants when the job closes - the exact class of bug BuildDetachedStartInfo_*
    // above guards against for OpenRGB, but *environment-imposed* rather than something
    // ProcessRunner does. UseShellExecute=true reliably escapes a normal desktop session's job
    // (verified manually - see docs/specs/05-lighting.md addendum, "OpenRGB auto-start
    // reliability"), but not a test host that never had a desktop shell to hand off to, so such a
    // test would be flaky here and in CI for reasons unrelated to whether the fix is correct.

    private const string PidsPrefix = "PIDS ";

    // Generous: PowerShell can take several seconds to start on a heavily loaded machine. These
    // bound how long a broken build hangs; they are not part of the synchronization.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    private const string ReportPidsThenWaitScript =
        "$p = Start-Process -FilePath ping.exe -ArgumentList '-n','60','127.0.0.1' -NoNewWindow -PassThru; " +
        "[Console]::Out.WriteLine('" + PidsPrefix + "' + $PID + ' ' + $p.Id); [Console]::Out.Flush(); " +
        "$p.WaitForExit()";

    private static bool TryParsePids(string line, out (int Child, int Grandchild) pids)
    {
        pids = default;
        if (!line.StartsWith(PidsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = line[PidsPrefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var child)
            && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var grandchild))
        {
            pids = (child, grandchild);
            return true;
        }

        return false;
    }

    private static Process OpenTracked(int pid)
    {
        var process = Process.GetProcessById(pid);
        _ = process.SafeHandle; // opens and caches the handle, pinning this process's identity
        return process;
    }

    private static void KillIfRunning(Process process)
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
            // Exited between HasExited and Kill(); nothing left to clean up.
        }
    }

    private sealed class RecordingProgress(List<string> target) : IProgress<string>
    {
        public void Report(string value) => target.Add(value);
    }

    /// <summary>Invokes the callback synchronously on the reporting thread (unlike
    /// <see cref="Progress{T}"/>, which posts to the thread pool).</summary>
    private sealed class CallbackProgress(Action<string> callback) : IProgress<string>
    {
        public void Report(string value) => callback(value);
    }
}
