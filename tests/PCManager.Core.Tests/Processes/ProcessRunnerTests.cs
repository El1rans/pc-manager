using PCManager.Core.Processes;
using Xunit;

namespace PCManager.Core.Tests.Processes;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_EchoesOutput_ReturnsExitCodeAndLines()
    {
        var runner = new ProcessRunner();
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
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            "cmd.exe", ["/c", "exit 7"], onLine: null, onProgress: null, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_Cancelled_ThrowsAndKillsProcess()
    {
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync(
            "cmd.exe", ["/c", "ping -n 30 127.0.0.1 >nul"], onLine: null, onProgress: null, cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RunAsync_StderrOutput_IsReportedToOnLine()
    {
        var runner = new ProcessRunner();
        var lines = new List<string>();

        await runner.RunAsync(
            "cmd.exe",
            ["/c", "echo oops 1>&2"],
            new RecordingProgress(lines),
            onProgress: null,
            TestContext.Current.CancellationToken);

        Assert.Contains(lines, l => l.Contains("oops", StringComparison.Ordinal));
    }

    private sealed class RecordingProgress(List<string> target) : IProgress<string>
    {
        public void Report(string value) => target.Add(value);
    }
}
