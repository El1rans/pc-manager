using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.RunningApps;
using Porchlight.Core.Startup;
using Porchlight.Core.Tests.Startup;
using Xunit;
using static Porchlight.Core.Tests.RunningApps.RunningAppsTestData;

namespace Porchlight.Core.Tests.RunningApps;

public sealed class RunningAppsServiceTests
{
    private const string WinDir = @"C:\Windows";
    private const int Self = 999;
    private const string ChromePath = @"C:\Chrome\chrome.exe";

    private readonly FakeProcessSnapshotSource _source = new();
    private readonly FakeProcessKiller _killer = new();
    private readonly FakeSystemMemoryInfo _memory = new();
    private readonly FakeFileProductInfoReader _info = new();
    private readonly FakeTimeProvider _time = new();

    private RunningAppsService Create() =>
        new(_source, _killer, _memory, _info, _time, NullLogger<RunningAppsService>.Instance, WinDir, Self, processorCount: 4);

    private static string ChromeKey => ChromePath.ToLowerInvariant();

    private static ProcessSample Chrome(int pid, DateTime? start = null, double cpu = 0) =>
        Sample(pid, "chrome", ChromePath, window: true, start: start, cpuSeconds: cpu);

    [Fact]
    public async Task Sample_FirstTickHasZeroCpu_ThenMeasuresElapsedTime()
    {
        _source.Samples = [Chrome(1, cpu: 10)];
        var service = Create();

        var first = await service.SampleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, first.Apps.Single().CpuPercent);

        _time.Advance(TimeSpan.FromSeconds(2));
        _source.Samples = [Chrome(1, cpu: 12)];
        var second = await service.SampleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(25, second.Apps.Single().CpuPercent, 3);
        Assert.Equal(25, second.TotalCpuPercent, 3);
        Assert.Equal(new MemoryStatus(16_000, 6_000), second.Memory);
    }

    [Fact]
    public async Task Sample_FriendlyName_PrefersDescriptionThenProductThenFileName_AndIsCached()
    {
        _info.Infos[ChromePath] = new FileProductInfo("Google Chrome", "Chrome Product", "Google");
        _info.Infos[@"C:\P\prod.exe"] = new FileProductInfo(" ", "The Product", null);
        _source.Samples =
        [
            Chrome(1),
            Sample(2, "prod", @"C:\P\prod.exe"),
            Sample(3, "plain", @"C:\P\plain.exe"),
            Sample(4, "nopath", null),
        ];
        var service = Create();

        var snapshot = await service.SampleAsync(TestContext.Current.CancellationToken);
        await service.SampleAsync(TestContext.Current.CancellationToken);

        var names = snapshot.Apps.Select(a => a.Name).Order().ToArray();
        Assert.Equal(["Google Chrome", "nopath", "plain", "The Product"], names);
        Assert.Equal(1, _info.Requested.Count(p => p == ChromePath));
    }

    [Fact]
    public async Task End_UnknownGroup_NotFound()
    {
        _source.Samples = [Chrome(1)];
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.NotFound, await service.EndAsync("nope", TestContext.Current.CancellationToken));
        Assert.Empty(_killer.Calls);
    }

    [Fact]
    public async Task End_BeforeAnySample_NotFound()
    {
        Assert.Equal(EndTaskResult.NotFound, await Create().EndAsync(ChromeKey, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("svchost", @"C:\Windows\System32\svchost.exe", 0)]
    [InlineData("explorer", @"D:\x\explorer.exe", 1)]
    [InlineData("notepad", @"C:\Windows\notepad.exe", 1)]
    [InlineData("Porchlight", @"C:\P\Porchlight.exe", Self)]
    public async Task End_ProtectedGroup_RefusedAndNothingKilled(string name, string path, int pid)
    {
        _source.Samples = [Sample(pid == 0 ? 5 : pid, name, path, session: pid == 0 ? 0 : 1)];
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        var result = await service.EndAsync(path.ToLowerInvariant(), TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.Refused, result);
        Assert.Empty(_killer.Calls);
    }

    [Fact]
    public async Task End_KillsEveryPidWithItsSnapshotStartTime()
    {
        var other = Started.AddHours(1);
        _source.Samples = [Chrome(1), Chrome(2, other)];
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        var result = await service.EndAsync(ChromeKey, TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.Ended, result);
        Assert.Equal([(1, (DateTime?)Started), (2, (DateTime?)other)], _killer.Calls);
    }

    [Fact]
    public async Task End_OnlyPidsFromTheLatestSampleAreKilled()
    {
        _source.Samples = [Chrome(1), Chrome(2)];
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);
        _source.Samples = [Chrome(1)];
        await service.SampleAsync(TestContext.Current.CancellationToken);

        await service.EndAsync(ChromeKey, TestContext.Current.CancellationToken);

        Assert.Equal([1], _killer.Calls.Select(c => c.Pid));
    }

    [Fact]
    public async Task End_StartTimeMismatchAndAlreadyExited_AreTreatedAsEnded()
    {
        _source.Samples = [Chrome(1), Chrome(2), Chrome(3)];
        _killer.Outcomes[1] = ProcessKillOutcome.StartTimeMismatch;
        _killer.Outcomes[2] = ProcessKillOutcome.AlreadyExited;
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        var result = await service.EndAsync(ChromeKey, TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.Ended, result);
        Assert.Equal(3, _killer.Calls.Count);
    }

    [Fact]
    public async Task End_AccessDenied_NeedsAdmin_EvenWhenOthersFail()
    {
        _source.Samples = [Chrome(1), Chrome(2), Chrome(3)];
        _killer.Outcomes[1] = ProcessKillOutcome.Failed;
        _killer.Outcomes[2] = ProcessKillOutcome.AccessDenied;
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        var result = await service.EndAsync(ChromeKey, TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.NeedsAdmin, result);
        Assert.Equal(3, _killer.Calls.Count);
    }

    [Fact]
    public async Task End_OtherFailure_Failed()
    {
        _source.Samples = [Chrome(1)];
        _killer.Outcomes[1] = ProcessKillOutcome.Failed;
        var service = Create();
        await service.SampleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EndTaskResult.Failed, await service.EndAsync(ChromeKey, TestContext.Current.CancellationToken));
    }
}
