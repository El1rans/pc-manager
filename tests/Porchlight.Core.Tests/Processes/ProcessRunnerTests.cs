using System.ComponentModel;
using System.Diagnostics;
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
        using var cts = new CancellationTokenSource();

        // cmd.exe spawns PING.EXE as a child; entireProcessTree:true must kill both.
        var runTask = runner.RunAsync(
            "cmd.exe", ["/c", "ping -n 30 127.0.0.1 >nul"], onLine: null, onProgress: null, cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        // Give the OS a moment to finish tearing down the child before asserting it is gone.
        for (var attempt = 0; attempt < 20 && Process.GetProcessesByName("PING").Length > 0; attempt++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Empty(Process.GetProcessesByName("PING"));
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

    private sealed class RecordingProgress(List<string> target) : IProgress<string>
    {
        public void Report(string value) => target.Add(value);
    }
}
