using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Processes;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetClientTests
{
    [Fact]
    public async Task GetUpgradesAsync_ParsesProcessOutput()
    {
        var runner = new FakeProcessRunner
        {
            NextLines =
            [
                "Name     Id       Version Available Source",
                "---------------------------------------------",
                "OBS      Obs.Obs  1.0     2.0       winget",
            ],
        };
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        var packages = await client.GetUpgradesAsync(includeUnknown: false, progress: null, CancellationToken.None);

        Assert.Single(packages);
        Assert.Equal("Obs.Obs", packages[0].Id);
        Assert.Equal("winget", runner.RunCalls[0].FileName);
        Assert.Equal(["upgrade", "--accept-source-agreements", "--disable-interactivity"], runner.RunCalls[0].Arguments);
    }

    [Fact]
    public async Task GetUpgradesAsync_IncludeUnknown_AddsFlag()
    {
        var runner = new FakeProcessRunner();
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await client.GetUpgradesAsync(includeUnknown: true, progress: null, CancellationToken.None);

        Assert.Contains("--include-unknown", runner.RunCalls[0].Arguments);
    }

    [Fact]
    public async Task UpgradeAsync_BuildsExpectedArguments()
    {
        var runner = new FakeProcessRunner { NextExitCode = 0 };
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        var result = await client.UpgradeAsync("Some.Id", silent: true, log: null, progress: null, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                "upgrade", "--id", "Some.Id", "--exact", "--include-unknown",
                "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity", "--silent",
            ],
            runner.RunCalls[0].Arguments);
    }

    [Fact]
    public async Task UpgradeAsync_NotSilent_OmitsSilentFlag()
    {
        var runner = new FakeProcessRunner();
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await client.UpgradeAsync("Some.Id", silent: false, log: null, progress: null, CancellationToken.None);

        Assert.DoesNotContain("--silent", runner.RunCalls[0].Arguments);
    }

    [Fact]
    public async Task ShowAsync_BuildsExpectedArguments()
    {
        var runner = new FakeProcessRunner();
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await client.ShowAsync("Some.Id", log: null, CancellationToken.None);

        Assert.Equal(
            ["show", "--id", "Some.Id", "--exact", "--accept-source-agreements", "--disable-interactivity"],
            runner.RunCalls[0].Arguments);
    }

    [Fact]
    public async Task GetUpgradesAsync_WingetMissing_ThrowsWingetNotFoundException()
    {
        var runner = new ThrowingProcessRunner(nativeErrorCode: 2); // ERROR_FILE_NOT_FOUND
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await Assert.ThrowsAsync<WingetNotFoundException>(
            () => client.GetUpgradesAsync(includeUnknown: false, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task GetUpgradesAsync_OtherWin32Error_PropagatesUnchanged()
    {
        // Not "file not found" - e.g. access denied - must not be misreported as "winget missing".
        var runner = new ThrowingProcessRunner(nativeErrorCode: 5); // ERROR_ACCESS_DENIED
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await Assert.ThrowsAsync<Win32Exception>(
            () => client.GetUpgradesAsync(includeUnknown: false, progress: null, CancellationToken.None));
    }

    /// <summary>Minimal fake standing in for <see cref="IProcessRunner"/> in these tests (a
    /// duplicate of <c>Components.FakeProcessRunner</c> kept local to this folder rather than
    /// shared, since test fakes are cheap to keep separate per feature area).</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public int NextExitCode { get; set; }

        public IReadOnlyList<string> NextLines { get; set; } = [];

        public List<(string FileName, IReadOnlyList<string> Arguments)> RunCalls { get; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IProgress<string>? onLine,
            IProgress<string>? onProgress,
            CancellationToken cancellationToken)
        {
            RunCalls.Add((fileName, arguments));
            return Task.FromResult(new ProcessRunResult(NextExitCode, NextLines, []));
        }

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingProcessRunner(int nativeErrorCode) : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IProgress<string>? onLine,
            IProgress<string>? onProgress,
            CancellationToken cancellationToken) =>
            throw new Win32Exception(nativeErrorCode, "Simulated Win32 failure.");

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
            throw new NotSupportedException();
    }
}
