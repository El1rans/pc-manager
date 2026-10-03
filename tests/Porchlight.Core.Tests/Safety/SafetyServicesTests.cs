using Porchlight.Core.Components;
using Porchlight.Core.Safety;
using Xunit;

namespace Porchlight.Core.Tests.Safety;

public sealed class SafetyServicesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RemoteAccess_MarksPorchlightsAnyDesk()
    {
        var probe = new FakeProbe(new RemoteToolEvidence(["AnyDesk", "TeamViewer"], [], []));
        var service = new RemoteAccessService(probe, new FakeComponents(ComponentState.Running));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.True(status.Tools.Single(t => t.Id == ComponentIds.AnyDesk).SetUpByPorchlight);
        Assert.False(status.Tools.Single(t => t.Id == "teamviewer").SetUpByPorchlight);
        Assert.Equal(SafetyLevel.Attention, status.Level);
        Assert.Equal(["TeamViewer"], status.OtherTools.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task RemoteAccess_OnlyPorchlightsAnyDesk_IsGood()
    {
        var probe = new FakeProbe(new RemoteToolEvidence([], ["AnyDesk"], []));
        var service = new RemoteAccessService(probe, new FakeComponents(ComponentState.Installed));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    [Fact]
    public async Task RemoteAccess_AnyDeskNotKnownToPorchlight_IsAWarning()
    {
        var probe = new FakeProbe(new RemoteToolEvidence(["AnyDesk"], [], []));
        var service = new RemoteAccessService(probe, new FakeComponents(ComponentState.NotInstalled));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SafetyLevel.Attention, status.Level);
        Assert.False(status.Tools.Single().SetUpByPorchlight);
    }

    [Fact]
    public async Task RemoteAccess_NothingFound_IsGood()
    {
        var service = new RemoteAccessService(new FakeProbe(new RemoteToolEvidence([], [], [])), new FakeComponents(ComponentState.NotInstalled));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Empty(status.Tools);
        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    [Fact]
    public async Task WindowsUpdate_UnreadableHistory_IsCouldNotCheck()
    {
        var service = new WindowsUpdateStatusService(new FakeAgent(history: null), new FixedTime(Now));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SafetyLevel.Unknown, status.Level);
    }

    [Fact]
    public async Task WindowsUpdate_GetAsync_NeverSearches()
    {
        var agent = new FakeAgent([new UpdateHistoryEntry(Now.AddDays(-3), "Cumulative", UpdateHistoryResult.Succeeded)]);
        var service = new WindowsUpdateStatusService(agent, new FixedTime(Now));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SafetyLevel.Good, status.Level);
        Assert.Equal(0, agent.SearchCalls);
    }

    [Fact]
    public async Task WindowsUpdate_CheckPending_ReportsCount()
    {
        var agent = new FakeAgent([]) { Pending = 3 };
        var service = new WindowsUpdateStatusService(agent, new FixedTime(Now));

        var result = await service.CheckPendingAsync(CancellationToken.None);

        Assert.Equal(PendingUpdatesOutcome.Found, result.Outcome);
        Assert.Equal("3 updates are waiting.", result.Message);
    }

    [Fact]
    public async Task WindowsUpdate_CheckPending_FailureIsReported()
    {
        var service = new WindowsUpdateStatusService(new FakeAgent([]) { Pending = null }, new FixedTime(Now));

        var result = await service.CheckPendingAsync(CancellationToken.None);

        Assert.Equal(PendingUpdatesOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Aggregate_CombinesAndCountsAttention()
    {
        var security = new SecurityStatusService(new FakeSecurityCenter(null), new FakeWindowsFirewall(null));
        var update = new WindowsUpdateStatusService(
            new FakeAgent([new UpdateHistoryEntry(Now.AddDays(-3), "Cumulative", UpdateHistoryResult.Succeeded)]), new FixedTime(Now));
        var remote = new RemoteAccessService(new FakeProbe(new RemoteToolEvidence(["rustdesk"], [], [])), new FakeComponents(ComponentState.NotInstalled));
        var service = new SafetyStatusService(security, update, remote);

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SafetyLevel.Unknown, status.Security.Level);
        Assert.Equal(1, status.AttentionCount);
        Assert.Equal("1 thing to look at", status.Headline);
    }

    [Fact]
    public void Summary_AllGood_SaysSafe()
    {
        var status = new SafetyStatus(
            SecurityVerdictBuilder.Build([], null) with { Level = SafetyLevel.Good },
            UpdateVerdictBuilder.Build(new UpdateHistorySummary(Now, 0), false, Now),
            new RemoteAccessStatus([]));

        Assert.Equal("This PC looks safe", status.Headline);
        Assert.Equal(SafetyLevel.Good, status.Level);
    }

    private sealed class FakeProbe(RemoteToolEvidence evidence) : IRemoteToolProbe
    {
        public Task<RemoteToolEvidence> CollectAsync(CancellationToken cancellationToken) => Task.FromResult(evidence);
    }

    [Fact]
    public async Task Security_UsesBothSources()
    {
        var off = new WindowsFirewallStatus(
            new Dictionary<WindowsFirewallProfile, bool> { [WindowsFirewallProfile.Public] = false },
            WindowsFirewallProfile.Public);
        var av = new SecurityProduct("AV", SecurityProductKind.Antivirus, new ProductStateInfo(ProductRunState.On, true));
        var service = new SecurityStatusService(new FakeSecurityCenter([av]), new FakeWindowsFirewall(off));

        var status = await service.GetAsync(CancellationToken.None);

        Assert.Equal(SecurityVerdictBuilder.FirewallOff, status.Verdict);
    }

    private sealed class FakeWindowsFirewall(WindowsFirewallStatus? status) : IWindowsFirewallReader
    {
        public Task<WindowsFirewallStatus?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(status);
    }

    private sealed class FakeSecurityCenter(IReadOnlyList<SecurityProduct>? products) : ISecurityCenterReader
    {
        public Task<IReadOnlyList<SecurityProduct>?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(products);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeAgent(IReadOnlyList<UpdateHistoryEntry>? history) : IWindowsUpdateAgent
    {
        public int? Pending { get; init; }

        public int SearchCalls { get; private set; }

        public Task<IReadOnlyList<UpdateHistoryEntry>?> ReadHistoryAsync(CancellationToken cancellationToken) => Task.FromResult(history);

        public Task<bool?> IsRestartPendingAsync(CancellationToken cancellationToken) => Task.FromResult<bool?>(false);

        public Task<int?> CountPendingAsync(CancellationToken cancellationToken)
        {
            SearchCalls++;
            return Task.FromResult(Pending);
        }
    }

    private sealed class FakeComponents(ComponentState anyDesk) : IComponentService
    {
        public IReadOnlyList<ComponentDefinition> Definitions => [];

        public event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged
        {
            add { }
            remove { }
        }

        public Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new ComponentStatus(anyDesk));

        public Task<ComponentStatus> InstallAsync(string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
