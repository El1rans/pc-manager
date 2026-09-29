using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Elevation;
using Porchlight.Core.Network;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.Core.Tests.Network;

public class NetworkRemedyServiceTests
{
    private sealed class FakeRunner : IProcessRunner
    {
        public List<string> Commands { get; } = [];

        public Func<string, int> ExitCodeFor { get; set; } = _ => 0;

        public Task<ProcessRunResult> RunAsync(
            string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine,
            IProgress<string>? onProgress, CancellationToken cancellationToken)
        {
            var command = fileName + " " + string.Join(' ', arguments);
            Commands.Add(command);
            return Task.FromResult(new ProcessRunResult(ExitCodeFor(command), [], []));
        }

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) =>
            throw new NotSupportedException();
    }

    private sealed class FakeElevation(bool elevated) : IElevationService
    {
        public bool IsElevated => elevated;

        public bool RestartElevated() => false;
    }

    private static NetworkRemedyService Create(FakeRunner runner, bool elevated) =>
        new(runner, new FakeElevation(elevated), NullLogger<NetworkRemedyService>.Instance);

    [Fact]
    public async Task Flush_dns_runs_ipconfig_flushdns()
    {
        var runner = new FakeRunner();
        var result = await Create(runner, false).FlushDnsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Done, result.Outcome);
        Assert.Equal(["ipconfig /flushdns"], runner.Commands);
    }

    [Fact]
    public async Task Renew_releases_then_renews()
    {
        var runner = new FakeRunner();
        var result = await Create(runner, false).RenewIpAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Done, result.Outcome);
        Assert.Equal(["ipconfig /release", "ipconfig /renew"], runner.Commands);
    }

    [Fact]
    public async Task Renew_failure_without_admin_asks_for_admin()
    {
        var runner = new FakeRunner { ExitCodeFor = c => c.EndsWith("/renew", StringComparison.Ordinal) ? 1 : 0 };
        var result = await Create(runner, false).RenewIpAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.NeedsAdmin, result.Outcome);
    }

    [Fact]
    public async Task Reset_without_admin_runs_nothing()
    {
        var runner = new FakeRunner();
        var result = await Create(runner, false).ResetAdapterAsync("Wi-Fi", TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.NeedsAdmin, result.Outcome);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task Reset_disables_then_enables()
    {
        var runner = new FakeRunner();
        var result = await Create(runner, true).ResetAdapterAsync("Wi-Fi 2", TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Done, result.Outcome);
        Assert.Equal(
            ["netsh interface set interface name=Wi-Fi 2 admin=disabled",
             "netsh interface set interface name=Wi-Fi 2 admin=enabled"],
            runner.Commands);
    }

    [Fact]
    public async Task Reset_always_re_enables_even_when_disable_fails()
    {
        var runner = new FakeRunner { ExitCodeFor = c => c.EndsWith("disabled", StringComparison.Ordinal) ? 1 : 0 };
        var result = await Create(runner, true).ResetAdapterAsync("Ethernet", TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.EndsWith("admin=enabled", runner.Commands[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Wi\"Fi")]
    [InlineData("Wi\nFi")]
    public async Task Reset_rejects_unsafe_names(string name)
    {
        var runner = new FakeRunner();
        var result = await Create(runner, true).ResetAdapterAsync(name, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.Empty(runner.Commands);
    }
}
