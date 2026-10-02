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
    private readonly FakeElevationService _elevation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private StartupService CreateService() =>
        new(_registry, _folders, _fileInfo, _startupInfo, _elevation, NullLogger<StartupService>.Instance, _time);

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
