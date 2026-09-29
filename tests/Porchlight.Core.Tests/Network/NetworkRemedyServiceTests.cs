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

    private sealed class FakeAdapters : INetworkAdapterController
    {
        public List<string> Calls { get; } = [];

        public bool DisableResult { get; set; } = true;

        public Exception? DisableThrows { get; set; }

        public bool EnableResult { get; set; } = true;

        public Task<bool> DisableAsync(string adapterId, CancellationToken cancellationToken)
        {
            Calls.Add("disable " + adapterId);
            return DisableThrows is null ? Task.FromResult(DisableResult) : Task.FromException<bool>(DisableThrows);
        }

        public Task<bool> EnableAsync(string adapterId, CancellationToken cancellationToken)
        {
            Calls.Add("enable " + adapterId);
            return Task.FromResult(EnableResult);
        }
    }

    private const string Guid1 = "{6b7a2f0e-1d0c-4a55-9d3e-2f6f7c0a1b11}";

    private static NetworkRemedyService Create(FakeRunner runner, bool elevated, FakeAdapters? adapters = null) =>
        new(runner, adapters ?? new FakeAdapters(), new FakeElevation(elevated), NullLogger<NetworkRemedyService>.Instance);

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
    public async Task Reset_without_admin_touches_nothing()
    {
        var adapters = new FakeAdapters();
        var result = await Create(new FakeRunner(), false, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.NeedsAdmin, result.Outcome);
        Assert.Empty(adapters.Calls);
    }

    [Fact]
    public async Task Reset_disables_then_enables_by_adapter_id()
    {
        var adapters = new FakeAdapters();
        var result = await Create(new FakeRunner(), true, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Done, result.Outcome);
        Assert.Equal(["disable " + Guid1, "enable " + Guid1], adapters.Calls);
    }

    [Fact]
    public async Task Reset_enables_even_when_disable_fails()
    {
        var adapters = new FakeAdapters { DisableResult = false };
        var result = await Create(new FakeRunner(), true, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.Equal("enable " + Guid1, adapters.Calls[^1]);
    }

    [Fact]
    public async Task Reset_enables_even_when_disable_throws_or_times_out()
    {
        var adapters = new FakeAdapters { DisableThrows = new TimeoutException() };
        var result = await Create(new FakeRunner(), true, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.Equal(["disable " + Guid1, "enable " + Guid1], adapters.Calls);
    }

    [Fact]
    public async Task Reset_enables_even_when_cancelled_during_disable()
    {
        var adapters = new FakeAdapters { DisableThrows = new OperationCanceledException() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(new FakeRunner(), true, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken));
        Assert.Equal("enable " + Guid1, adapters.Calls[^1]);
    }

    [Fact]
    public async Task Reset_reports_when_enable_fails()
    {
        var adapters = new FakeAdapters { EnableResult = false };
        var result = await Create(new FakeRunner(), true, adapters).ResetAdapterAsync(Guid1, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.Contains("back on", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Wi-Fi")]
    [InlineData("x' OR 1=1 --")]
    public async Task Reset_rejects_ids_that_are_not_guids(string id)
    {
        var adapters = new FakeAdapters();
        var result = await Create(new FakeRunner(), true, adapters).ResetAdapterAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(RemedyOutcome.Failed, result.Outcome);
        Assert.Empty(adapters.Calls);
    }
}
