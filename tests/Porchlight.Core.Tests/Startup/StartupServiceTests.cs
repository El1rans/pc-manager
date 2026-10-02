using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Startup;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupServiceTests
{
    private const string AppPath = @"C:\Tools\Foo\foo.exe";

    private readonly FakeStartupRegistry _registry = new();
    private readonly FakeStartupFolderReader _folders = new();
    private readonly FakeFileProductInfoReader _fileInfo = new();
    private readonly FakeStartupInfoReader _startupInfo = new();
    private readonly FakeLogonTaskSource _tasks = new();
    private readonly FakeElevationService _elevation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private StartupService CreateService() =>
        new(_registry, _folders, _fileInfo, _startupInfo, _tasks, _elevation, NullLogger<StartupService>.Instance, _time);

    private void AddRunValue(StartupSource source, string name, string command)
    {
        if (!_registry.RunValues.TryGetValue(source, out var list))
        {
            list = [];
            _registry.RunValues[source] = list;
        }

        list.Add(new StartupRunValue(name, command));
    }

    [Fact]
    public async Task ListAsync_ReadsEveryRunSourceAndFolder()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", $"\"{AppPath}\"");
        AddRunValue(StartupSource.MachineRun, "B", @"C:\Tools\b.exe");
        AddRunValue(StartupSource.MachineRun32, "C", @"C:\Tools\c.exe");
        _folders.Items[StartupSource.CurrentUserFolder] = [new StartupFolderItem("Photos.lnk", @"C:\Tools\photos.exe")];
        _folders.Items[StartupSource.MachineFolder] = [new StartupFolderItem("Shared.lnk", null)];

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, entries.Count);
        Assert.Contains(entries, e => e.Source == StartupSource.MachineRun32 && e.ItemName == "C");
        Assert.Contains(entries, e => e.Source == StartupSource.MachineFolder && e.DisplayName == "Shared");
    }

    [Fact]
    public async Task ListAsync_NameAndPublisherComeFromFileInfoWithFallbacks()
    {
        AddRunValue(StartupSource.CurrentUserRun, "Described", AppPath);
        AddRunValue(StartupSource.CurrentUserRun, "ProductOnly", @"C:\Tools\p.exe");
        AddRunValue(StartupSource.CurrentUserRun, "Bare", "rundll32.exe something");
        _fileInfo.Infos[AppPath] = new FileProductInfo("Foo Helper", "Foo", "Foo Inc");
        _fileInfo.Infos[@"C:\Tools\p.exe"] = new FileProductInfo(" ", "Prod", null);

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        var described = entries.Single(e => e.ItemName == "Described");
        Assert.Equal("Foo Helper", described.DisplayName);
        Assert.Equal("Foo Inc", described.Publisher);
        Assert.Equal("Prod", entries.Single(e => e.ItemName == "ProductOnly").DisplayName);
        var bare = entries.Single(e => e.ItemName == "Bare");
        Assert.Equal("Bare", bare.DisplayName);
        Assert.Null(bare.ExecutablePath);
        Assert.DoesNotContain("rundll32.exe", _fileInfo.Requested);
    }

    [Fact]
    public async Task ListAsync_AttachesImpactFromTheStartupTrace()
    {
        AddRunValue(StartupSource.CurrentUserRun, "Heavy", AppPath);
        AddRunValue(StartupSource.CurrentUserRun, "Light", @"C:\Tools\light.exe");
        AddRunValue(StartupSource.CurrentUserRun, "Unknown", @"C:\Tools\unknown.exe");
        _startupInfo.Records.Add(new StartupInfoRecord(AppPath, 2000, 0));
        _startupInfo.Records.Add(new StartupInfoRecord(@"C:\Tools\light.exe", 10, 10));
        var service = CreateService();

        var entries = await service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StartupImpact.High, entries.Single(e => e.ItemName == "Heavy").Impact);
        Assert.Equal(StartupImpact.Low, entries.Single(e => e.ItemName == "Light").Impact);
        Assert.Equal(StartupImpact.NotMeasured, entries.Single(e => e.ItemName == "Unknown").Impact);
        Assert.False(service.ImpactNeedsAdmin);
    }

    [Fact]
    public async Task ListAsync_TraceAccessDenied_EverythingNotMeasuredAndFlagged()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", AppPath);
        _startupInfo.AccessDenied = true;
        var service = CreateService();

        var entries = await service.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StartupImpact.NotMeasured, Assert.Single(entries).Impact);
        Assert.True(service.ImpactNeedsAdmin);
    }

    [Fact]
    public async Task ListAsync_ListsLogonTasksAsTheirOwnSource()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Sync", "Sync", AppPath, IsEnabled: true, IsMachineWide: false));
        _tasks.Tasks.Add(new LogonTask(@"\Off", "Off", null, IsEnabled: false, IsMachineWide: false));
        _fileInfo.Infos[AppPath] = new FileProductInfo("Foo Sync", "Foo", "Foo Inc");
        _startupInfo.Records.Add(new StartupInfoRecord(AppPath, 2000, 0));

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        var sync = entries.Single(e => e.Id == @"LogonTask|\Vendor\Sync");
        Assert.Equal(StartupSource.LogonTask, sync.Source);
        Assert.Equal("Scheduled task", sync.Source.ToLabel());
        Assert.Equal("Foo Sync", sync.DisplayName);
        Assert.Equal("Foo Inc", sync.Publisher);
        Assert.True(sync.IsEnabled);
        Assert.Equal(StartupImpact.High, sync.Impact);
        Assert.False(sync.RequiresAdmin);
        var off = entries.Single(e => e.ItemName == @"\Off");
        Assert.Equal("Off", off.DisplayName);
        Assert.False(off.IsEnabled);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public async Task ListAsync_HidesPorchlightsOwnLoginTask()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Porchlight", "Porchlight", AppPath, IsEnabled: true, IsMachineWide: false));
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Sync", "Sync", AppPath, IsEnabled: true, IsMachineWide: false));

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(entries, e => e.Id == @"LogonTask|\Porchlight");
        Assert.Contains(entries, e => e.Id == @"LogonTask|\Vendor\Sync");
    }

    [Fact]
    public async Task ListAsync_MicrosoftFolderTask_IsRecommendedToKeep()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Microsoft\Office\Telemetry", "Telemetry", @"C:\Tools\x.exe", true, false));
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Other", "Other", @"C:\Tools\y.exe", true, false));

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.True(entries.Single(e => e.ItemName.StartsWith(@"\Microsoft\", StringComparison.Ordinal)).RecommendedToKeep);
        Assert.False(entries.Single(e => e.ItemName == @"\Vendor\Other").RecommendedToKeep);
    }

    [Fact]
    public async Task ListAsync_TaskSchedulerUnreadable_IsSkippedNotFatal()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", AppPath);
        _tasks.ReadException = new IOException("scheduler down");

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StartupSource.CurrentUserRun, Assert.Single(entries).Source);
    }

    [Fact]
    public async Task SetEnabledAsync_Task_WritesOnlyTheEnabledFlag()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Sync", "Sync", AppPath, true, false));
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var off = await service.SetEnabledAsync(@"LogonTask|\Vendor\Sync", false, TestContext.Current.CancellationToken);
        var on = await service.SetEnabledAsync(@"LogonTask|\Vendor\Sync", true, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Changed, off);
        Assert.Equal(StartupChangeResult.Changed, on);
        Assert.Equal([(@"\Vendor\Sync", false), (@"\Vendor\Sync", true)], _tasks.Changes);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public async Task SetEnabledAsync_MachineWideTaskWhenNotElevated_IsRefusedWithoutChange()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Svc", "Svc", AppPath, true, IsMachineWide: true));
        _elevation.IsElevated = false;
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync(@"LogonTask|\Vendor\Svc", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NeedsAdmin, result);
        Assert.Empty(_tasks.Changes);
    }

    [Fact]
    public async Task SetEnabledAsync_MachineWideTaskWhenElevated_Changes()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Svc", "Svc", AppPath, true, IsMachineWide: true));
        _elevation.IsElevated = true;
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync(@"LogonTask|\Vendor\Svc", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Changed, result);
    }

    [Fact]
    public async Task SetEnabledAsync_TaskAccessDenied_ReturnsNeedsAdmin()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Sync", "Sync", AppPath, true, false));
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);
        _tasks.SetException = new UnauthorizedAccessException();

        var result = await service.SetEnabledAsync(@"LogonTask|\Vendor\Sync", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NeedsAdmin, result);
    }

    [Fact]
    public async Task SetEnabledAsync_TaskOtherFailure_ReturnsFailed()
    {
        _tasks.Tasks.Add(new LogonTask(@"\Vendor\Sync", "Sync", AppPath, true, false));
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);
        _tasks.SetException = new IOException("gone");

        var result = await service.SetEnabledAsync(@"LogonTask|\Vendor\Sync", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Failed, result);
    }

    [Fact]
    public async Task SetEnabledAsync_TaskNotInLastListing_IsRefused()
    {
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync(@"LogonTask|\Anything", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NotFound, result);
        Assert.Empty(_tasks.Changes);
    }

    [Theory]
    [InlineData(@"\Microsoft\Windows", true)]
    [InlineData(@"\Microsoft\Windows\Defrag", true)]
    [InlineData(@"\microsoft\windows\x\y", true)]
    [InlineData(@"\Microsoft", false)]
    [InlineData(@"\Microsoft\Office", false)]
    [InlineData(@"\Microsoft\WindowsApps", false)]
    [InlineData(@"\", false)]
    public void LogonTaskSource_SkipsOnlyTheWindowsOwnTree(string folder, bool skipped)
    {
        Assert.Equal(skipped, LogonTaskSource.IsWindowsOwn(folder));
    }

    [Fact]
    public async Task ListAsync_ReadsDisabledStateFromApproval()
    {
        AddRunValue(StartupSource.CurrentUserRun, "On", AppPath);
        AddRunValue(StartupSource.CurrentUserRun, "Off", AppPath);
        _registry.Approvals[(StartupSource.CurrentUserRun, "Off")] = StartupApprovedBlob.CreateDisabled(_time.GetUtcNow());

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.True(entries.Single(e => e.ItemName == "On").IsEnabled);
        Assert.False(entries.Single(e => e.ItemName == "Off").IsEnabled);
    }

    [Fact]
    public async Task ListAsync_UnreadableSource_IsSkippedNotFatal()
    {
        _registry.ReadRunException = new UnauthorizedAccessException();
        _folders.Items[StartupSource.CurrentUserFolder] = [new StartupFolderItem("x.lnk", null)];

        var entries = await CreateService().ListAsync(TestContext.Current.CancellationToken);

        Assert.Single(entries);
    }

    [Fact]
    public async Task SetEnabledAsync_DisableUserEntry_WritesDisabledBlobToThatSourceOnly()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", AppPath);
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync("CurrentUserRun|A", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Changed, result);
        var write = Assert.Single(_registry.Writes);
        Assert.Equal((StartupSource.CurrentUserRun, "A"), (write.Source, write.Name));
        Assert.Equal(StartupApprovedBlob.CreateDisabled(_time.GetUtcNow()), write.Value);
    }

    [Fact]
    public async Task SetEnabledAsync_Enable_WritesEnabledBlob()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", AppPath);
        _registry.Approvals[(StartupSource.CurrentUserRun, "A")] = StartupApprovedBlob.CreateDisabled(_time.GetUtcNow());
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        await service.SetEnabledAsync("CurrentUserRun|A", true, TestContext.Current.CancellationToken);

        Assert.Equal(StartupApprovedBlob.CreateEnabled(), Assert.Single(_registry.Writes).Value);
    }

    [Theory]
    [InlineData(StartupSource.MachineRun)]
    [InlineData(StartupSource.MachineRun32)]
    public async Task SetEnabledAsync_MachineEntryWhenNotElevated_IsRefusedWithoutWriting(StartupSource source)
    {
        AddRunValue(source, "M", AppPath);
        _elevation.IsElevated = false;
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync($"{source}|M", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NeedsAdmin, result);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public async Task SetEnabledAsync_MachineFolderEntryWhenElevated_Writes()
    {
        _folders.Items[StartupSource.MachineFolder] = [new StartupFolderItem("Shared.lnk", null)];
        _elevation.IsElevated = true;
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync("MachineFolder|Shared.lnk", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Changed, result);
        Assert.Equal(StartupSource.MachineFolder, Assert.Single(_registry.Writes).Source);
    }

    [Fact]
    public async Task SetEnabledAsync_UnknownId_IsRefused()
    {
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);

        var result = await service.SetEnabledAsync("CurrentUserRun|NotListed", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NotFound, result);
        Assert.Empty(_registry.Writes);
    }

    [Fact]
    public async Task SetEnabledAsync_BeforeAnyListing_IsRefused()
    {
        var result = await CreateService().SetEnabledAsync("CurrentUserRun|A", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.NotFound, result);
    }

    [Fact]
    public async Task SetEnabledAsync_WriteFails_ReturnsFailed()
    {
        AddRunValue(StartupSource.CurrentUserRun, "A", AppPath);
        var service = CreateService();
        await service.ListAsync(TestContext.Current.CancellationToken);
        _registry.WriteException = new UnauthorizedAccessException();

        var result = await service.SetEnabledAsync("CurrentUserRun|A", false, TestContext.Current.CancellationToken);

        Assert.Equal(StartupChangeResult.Failed, result);
    }
}
