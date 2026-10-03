using Porchlight.Core.Changes;
using Porchlight.Core.Startup;
using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.Core.Tests.Changes;

public sealed class UndoerTests
{
    [Fact]
    public async Task StartupUndoer_ListsThenPutsTheOldStateBack()
    {
        var startup = new FakeStartup();
        var undoer = new StartupChangeUndoer(startup);

        var result = await undoer.UndoAsync(
            StartupChangeUndoer.CreatePayload("CurrentUserRun|Foo", "Foo", enabledBefore: true), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["list", "set CurrentUserRun|Foo True"], startup.Calls);
    }

    [Theory]
    [InlineData(StartupChangeResult.NotFound)]
    [InlineData(StartupChangeResult.NeedsAdmin)]
    [InlineData(StartupChangeResult.Failed)]
    public async Task StartupUndoer_FailuresAreReported(StartupChangeResult result)
    {
        var undoer = new StartupChangeUndoer(new FakeStartup { Result = result });

        var undo = await undoer.UndoAsync(StartupChangeUndoer.CreatePayload("id", "Foo", false), TestContext.Current.CancellationToken);

        Assert.False(undo.Succeeded);
    }

    [Fact]
    public async Task StartupUndoer_BadPayloadIsRefusedWithoutTouchingAnything()
    {
        var startup = new FakeStartup();

        var undo = await new StartupChangeUndoer(startup).UndoAsync("not json", TestContext.Current.CancellationToken);

        Assert.False(undo.Succeeded);
        Assert.Empty(startup.Calls);
    }

    [Fact]
    public async Task ServiceStartTypeUndoer_RestoresThePreviousStartTypeIncludingDelayed()
    {
        var services = new FakeServices();
        var undoer = new ServiceStartTypeUndoer(services);

        var result = await undoer.UndoAsync(
            ServiceStartTypeUndoer.CreatePayload("Foo", "Foo display", ServiceStartType.AutomaticDelayed), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["list", $"type Foo {ServiceStartType.AutomaticDelayed}"], services.Calls);
    }

    [Theory]
    [InlineData(true, "start Foo")]
    [InlineData(false, "stop Foo")]
    public async Task ServiceStateUndoer_DoesTheOpposite(bool runningBefore, string expectedCall)
    {
        var services = new FakeServices();

        var result = await new ServiceStateUndoer(services).UndoAsync(
            ServiceStateUndoer.CreatePayload("Foo", "Foo display", runningBefore), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["list", expectedCall], services.Calls);
    }

    [Theory]
    [InlineData(ServiceChangeResult.NeedsAdmin)]
    [InlineData(ServiceChangeResult.Refused)]
    [InlineData(ServiceChangeResult.NotFound)]
    [InlineData(ServiceChangeResult.TimedOut)]
    [InlineData(ServiceChangeResult.Failed)]
    public async Task ServiceUndoers_ReportFailuresInPlainWords(ServiceChangeResult failure)
    {
        var services = new FakeServices { Next = ServiceChangeOutcome.Of(failure) };

        var result = await new ServiceStartTypeUndoer(services).UndoAsync(
            ServiceStartTypeUndoer.CreatePayload("Foo", "Foo display", ServiceStartType.Manual), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    private sealed class FakeStartup : IStartupService
    {
        public List<string> Calls { get; } = [];

        public StartupChangeResult Result { get; set; } = StartupChangeResult.Changed;

        public bool ImpactNeedsAdmin => false;

        public Task<IReadOnlyList<StartupEntry>> ListAsync(CancellationToken cancellationToken)
        {
            Calls.Add("list");
            return Task.FromResult<IReadOnlyList<StartupEntry>>([]);
        }

        public Task<StartupChangeResult> SetEnabledAsync(string entryId, bool enabled, CancellationToken cancellationToken)
        {
            Calls.Add($"set {entryId} {enabled}");
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeServices : IWindowsServicesService
    {
        public List<string> Calls { get; } = [];

        public ServiceChangeOutcome Next { get; set; } = ServiceChangeOutcome.Of(ServiceChangeResult.Changed);

        public Task<IReadOnlyList<WindowsServiceEntry>> ListAsync(CancellationToken cancellationToken)
        {
            Calls.Add("list");
            return Task.FromResult<IReadOnlyList<WindowsServiceEntry>>([]);
        }

        public Task<ServiceChangeOutcome> StartAsync(string name, CancellationToken cancellationToken) => Record($"start {name}");

        public Task<ServiceChangeOutcome> StopAsync(string name, CancellationToken cancellationToken) => Record($"stop {name}");

        public Task<ServiceChangeOutcome> RestartAsync(string name, CancellationToken cancellationToken) => Record($"restart {name}");

        public Task<ServiceChangeOutcome> SetStartTypeAsync(string name, ServiceStartType startType, CancellationToken cancellationToken) =>
            Record($"type {name} {startType}");

        private Task<ServiceChangeOutcome> Record(string call)
        {
            Calls.Add(call);
            return Task.FromResult(Next);
        }
    }
}
