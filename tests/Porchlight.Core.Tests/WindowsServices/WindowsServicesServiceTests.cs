using Porchlight.Core.Startup;
using Porchlight.Core.Tests.Components;
using Porchlight.Core.Tests.Startup;
using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.Core.Tests.WindowsServices;

public sealed class WindowsServicesServiceTests
{
    private const string ThirdPartyExe = @"C:\Program Files\Foo\foo.exe";
    private static readonly string WindowsExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "svchost.exe");

    private readonly FakeServiceInfoSource _source = new();
    private readonly FakeServiceManager _manager = new();
    private readonly FakeFileProductInfoReader _fileInfo = new();
    private readonly FakeElevationService _elevation = new() { IsElevated = true };

    private WindowsServicesService Create() => new(_source, _manager, _fileInfo, _elevation);

    private static ServiceRawInfo Raw(string name, string path, string? start = "Auto", bool delayed = false, string type = "Own Process") =>
        new(name, name + " display", "Does a thing. More text.", path, start, delayed, "Running", type);

    private async Task<WindowsServicesService> CreateListedAsync()
    {
        _source.Items =
        [
            Raw("Foo", $"\"{ThirdPartyExe}\" -svc"),
            Raw("Bar", WindowsExe + " -k netsvcs"),
            Raw("MsApp", @"C:\Program Files\Ms\ms.exe"),
            Raw("PawnIO", @"C:\Program Files\PawnIO\pawn.exe"),
            Raw("Drv", @"\SystemRoot\System32\drivers\drv.sys", type: "Kernel Driver"),
        ];
        _fileInfo.Infos[ThirdPartyExe] = new FileProductInfo("Foo", "Foo", "Foo Inc");
        _fileInfo.Infos[@"C:\Program Files\Ms\ms.exe"] = new FileProductInfo("Ms", "Ms", "Microsoft Corporation");
        var service = Create();
        await service.ListAsync(TestContext.Current.CancellationToken);
        return service;
    }

    [Fact]
    public async Task ListAsync_ClassifiesAndSkipsDrivers()
    {
        var service = Create();
        _source.Items =
        [
            Raw("Foo", $"\"{ThirdPartyExe}\" -svc", "Auto", delayed: true),
            Raw("Bar", WindowsExe + " -k netsvcs"),
            Raw("Drv", @"\SystemRoot\System32\drivers\drv.sys", type: "Kernel Driver"),
        ];
        _fileInfo.Infos[ThirdPartyExe] = new FileProductInfo("Foo", "Foo", " Foo Inc ");

        var entries = await service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Count);
        var foo = entries.Single(e => e.Name == "Foo");
        Assert.False(foo.IsMicrosoft);
        Assert.True(foo.IsChangeable);
        Assert.Equal("Foo Inc", foo.Publisher);
        Assert.Equal("Does a thing.", foo.Description);
        Assert.Equal(ServiceStartType.AutomaticDelayed, foo.StartType);
        Assert.Equal(ServiceRunState.Running, foo.State);
        var bar = entries.Single(e => e.Name == "Bar");
        Assert.True(bar.IsMicrosoft);
        Assert.False(bar.IsChangeable);
    }

    [Fact]
    public async Task ListAsync_ComponentServiceIsNotChangeable()
    {
        var service = await CreateListedAsync();

        var entries = await service.ListAsync(TestContext.Current.CancellationToken);

        var pawn = entries.Single(e => e.Name == "PawnIO");
        Assert.False(pawn.IsMicrosoft);
        Assert.False(pawn.IsChangeable);
    }

    [Fact]
    public async Task Change_ThirdPartyService_CallsManager()
    {
        var service = await CreateListedAsync();
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(ServiceChangeResult.Changed, (await service.StartAsync("Foo", ct)).Result);
        Assert.Equal(ServiceChangeResult.Changed, (await service.StopAsync("Foo", ct)).Result);
        Assert.Equal(ServiceChangeResult.Changed, (await service.SetStartTypeAsync("Foo", ServiceStartType.Disabled, ct)).Result);

        Assert.Equal(["start Foo", "stop Foo", "type Foo Disabled"], _manager.Calls);
    }

    [Theory]
    [InlineData("Bar")]
    [InlineData("MsApp")]
    [InlineData("PawnIO")]
    [InlineData("Drv")]
    [InlineData("NotListed")]
    public async Task Change_RefusesMicrosoftComponentDriverAndUnknown(string name)
    {
        var service = await CreateListedAsync();
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(ServiceChangeResult.Refused, (await service.StartAsync(name, ct)).Result);
        Assert.Equal(ServiceChangeResult.Refused, (await service.StopAsync(name, ct)).Result);
        Assert.Equal(ServiceChangeResult.Refused, (await service.RestartAsync(name, ct)).Result);
        Assert.Equal(ServiceChangeResult.Refused, (await service.SetStartTypeAsync(name, ServiceStartType.Manual, ct)).Result);
        Assert.Empty(_manager.Calls);
    }

    [Fact]
    public async Task Change_NotElevated_NeedsAdminWithoutTouchingAnything()
    {
        var service = await CreateListedAsync();
        _elevation.IsElevated = false;
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(ServiceChangeResult.NeedsAdmin, (await service.StartAsync("Foo", ct)).Result);
        Assert.Equal(ServiceChangeResult.NeedsAdmin, (await service.SetStartTypeAsync("Foo", ServiceStartType.Manual, ct)).Result);
        Assert.Empty(_manager.Calls);
    }

    [Fact]
    public async Task Change_MicrosoftIsRefusedEvenWhenNotElevated()
    {
        var service = await CreateListedAsync();
        _elevation.IsElevated = false;

        var outcome = await service.StopAsync("Bar", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.Refused, outcome.Result);
    }

    [Fact]
    public async Task Stop_WithRunningDependents_IsRefusedAndNamesThem()
    {
        var service = await CreateListedAsync();
        _manager.Dependents = ["Alpha service", "Beta service"];

        var outcome = await service.StopAsync("Foo", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.HasDependents, outcome.Result);
        Assert.Equal(["Alpha service", "Beta service"], outcome.Dependents);
        Assert.DoesNotContain("stop Foo", _manager.Calls);
    }

    [Fact]
    public async Task Restart_StopsThenStarts()
    {
        var service = await CreateListedAsync();

        var outcome = await service.RestartAsync("Foo", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.Changed, outcome.Result);
        Assert.Equal(["stop Foo", "start Foo"], _manager.Calls);
    }

    [Fact]
    public async Task Restart_StopTimeout_DoesNotStart()
    {
        var service = await CreateListedAsync();
        _manager.StopResult = ServiceChangeResult.TimedOut;

        var outcome = await service.RestartAsync("Foo", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.TimedOut, outcome.Result);
        Assert.Equal(["stop Foo"], _manager.Calls);
    }

    [Fact]
    public async Task Start_UsesTimeoutAndPassesManagerResultThrough()
    {
        var service = await CreateListedAsync();
        _manager.StartResult = ServiceChangeResult.TimedOut;

        var outcome = await service.StartAsync("Foo", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.TimedOut, outcome.Result);
        Assert.Equal(WindowsServicesService.ChangeTimeout, _manager.LastTimeout);
    }

    [Theory]
    [InlineData(ServiceStartType.Automatic)]
    [InlineData(ServiceStartType.AutomaticDelayed)]
    [InlineData(ServiceStartType.Manual)]
    [InlineData(ServiceStartType.Disabled)]
    public async Task SetStartType_PassesTheTypeThrough(ServiceStartType type)
    {
        var service = await CreateListedAsync();

        await service.SetStartTypeAsync("Foo", type, TestContext.Current.CancellationToken);

        Assert.Equal([$"type Foo {type}"], _manager.Calls);
    }

    [Fact]
    public async Task Change_ManagerFailure_IsReported()
    {
        var service = await CreateListedAsync();
        _manager.StartResult = ServiceChangeResult.Failed;

        var outcome = await service.StartAsync("Foo", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceChangeResult.Failed, outcome.Result);
    }

    private sealed class FakeServiceInfoSource : IServiceInfoSource
    {
        public IReadOnlyList<ServiceRawInfo> Items { get; set; } = [];

        public IReadOnlyList<ServiceRawInfo> ReadAll() => Items;
    }

    private sealed class FakeServiceManager : IServiceManager
    {
        public List<string> Calls { get; } = [];

        public IReadOnlyList<string> Dependents { get; set; } = [];

        public ServiceChangeResult StartResult { get; set; } = ServiceChangeResult.Changed;

        public ServiceChangeResult StopResult { get; set; } = ServiceChangeResult.Changed;

        public TimeSpan LastTimeout { get; private set; }

        public IReadOnlyList<string> GetRunningDependents(string name) => Dependents;

        public ServiceChangeResult Start(string name, TimeSpan timeout)
        {
            Calls.Add($"start {name}");
            LastTimeout = timeout;
            return StartResult;
        }

        public ServiceChangeResult Stop(string name, TimeSpan timeout)
        {
            Calls.Add($"stop {name}");
            LastTimeout = timeout;
            return StopResult;
        }

        public ServiceChangeResult SetStartType(string name, ServiceStartType startType)
        {
            Calls.Add($"type {name} {startType}");
            return ServiceChangeResult.Changed;
        }
    }
}
