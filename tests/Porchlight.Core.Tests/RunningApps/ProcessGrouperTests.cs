using Porchlight.Core.RunningApps;
using Xunit;
using static Porchlight.Core.Tests.RunningApps.RunningAppsTestData;

namespace Porchlight.Core.Tests.RunningApps;

public sealed class ProcessGrouperTests
{
    private const string WinDir = @"C:\Windows";
    private const int Self = 999;
    private static readonly Dictionary<int, double> NoCpu = [];

    private static IReadOnlyList<ProcessGroup> Group(IReadOnlyDictionary<int, double>? cpu, params ProcessSample[] samples) =>
        ProcessGrouper.Group(samples, cpu ?? NoCpu, WinDir, Self);

    [Fact]
    public void Group_ByPathCaseInsensitive_SumsCpuAndMemory()
    {
        var cpu = new Dictionary<int, double> { [1] = 1.5, [2] = 2.0, [3] = 4.0 };

        var groups = Group(
            cpu,
            Sample(1, "chrome", @"C:\Chrome\chrome.exe", memory: 100),
            Sample(2, "chrome", @"c:\chrome\CHROME.exe", memory: 200),
            Sample(3, "other", @"C:\Other\other.exe", memory: 50));

        Assert.Equal(2, groups.Count);
        var chrome = groups.Single(g => g.Members.Count == 2);
        Assert.Equal(3.5, chrome.CpuPercent, 3);
        Assert.Equal(300, chrome.MemoryBytes);
    }

    [Fact]
    public void Group_NoPath_FallsBackToName()
    {
        var groups = Group(null, Sample(1, "foo", null, window: true), Sample(2, "foo", null, window: true), Sample(3, "bar", null, window: true));

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups.Single(g => g.Name == "foo").Members.Count);
    }

    [Fact]
    public void Group_Sections()
    {
        var groups = Group(
            null,
            Sample(1, "word", @"C:\Office\word.exe", window: true),
            Sample(2, "helper", @"C:\Office\helper.exe"),
            Sample(3, "notepad", @"C:\Windows\System32\notepad.exe", window: true),
            Sample(4, "mystery", null, session: 0),
            Sample(5, "tray", null, session: 1));

        Assert.Equal(RunningAppSection.Apps, groups.Single(g => g.Name == "word").Section);
        Assert.Equal(RunningAppSection.Background, groups.Single(g => g.Name == "helper").Section);
        Assert.Equal(RunningAppSection.Windows, groups.Single(g => g.Name == "notepad").Section);
        Assert.Equal(RunningAppSection.Windows, groups.Single(g => g.Name == "mystery").Section);
        Assert.Equal(RunningAppSection.Background, groups.Single(g => g.Name == "tray").Section);
    }

    [Fact]
    public void Group_NormalApps_CanEnd()
    {
        var groups = Group(null, Sample(1, "word", @"C:\Office\word.exe", window: true));

        Assert.True(groups.Single().CanEnd);
    }

    [Theory]
    [InlineData("svchost")]
    [InlineData("LSASS")]
    [InlineData("csrss")]
    [InlineData("explorer")]
    [InlineData("dwm")]
    [InlineData("Registry")]
    [InlineData("MemCompression")]
    public void Group_CriticalName_CannotEndEvenOutsideWindowsFolder(string name)
    {
        var groups = Group(null, Sample(1, name, @"D:\Weird\" + name + ".exe", window: true));

        Assert.False(groups.Single().CanEnd);
    }

    [Fact]
    public void Group_WindowsFolder_CannotEnd()
    {
        var groups = Group(null, Sample(1, "notepad", @"C:\WINDOWS\System32\notepad.exe", window: true));

        Assert.False(groups.Single().CanEnd);
    }

    [Fact]
    public void Group_FolderNamedLikeWindows_IsNotTheWindowsFolder()
    {
        var groups = Group(null, Sample(1, "app", @"C:\WindowsApps\app.exe", window: true));

        Assert.Equal(RunningAppSection.Apps, groups.Single().Section);
        Assert.True(groups.Single().CanEnd);
    }

    [Fact]
    public void Group_ServicesSession_CannotEnd()
    {
        var groups = Group(null, Sample(1, "svc", @"C:\Svc\svc.exe", session: 0));

        Assert.False(groups.Single().CanEnd);
    }

    [Fact]
    public void Group_Porchlight_CannotEnd()
    {
        var groups = Group(null, Sample(Self, "Porchlight", @"C:\P\Porchlight.exe", window: true));

        Assert.False(groups.Single().CanEnd);
    }

    [Fact]
    public void Group_ProtectedMemberProtectsWholeGroup()
    {
        var groups = Group(null, Sample(1, "app", @"C:\A\app.exe"), Sample(Self, "app", @"C:\A\app.exe"));

        Assert.False(groups.Single().CanEnd);
    }
}
