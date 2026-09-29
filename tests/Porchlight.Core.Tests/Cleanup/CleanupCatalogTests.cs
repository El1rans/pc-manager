using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class CleanupCatalogTests
{
    private readonly FakeCleanupPathProvider _paths = new();
    private readonly FakeCleanupFileSystem _fs = new();

    private IReadOnlyList<CleanupCategory> Build() =>
        new CleanupCatalog(_paths, _fs, NullLogger<CleanupCatalog>.Instance).GetCategories();

    private CleanupCategory Get(CleanupCategoryId id) => Assert.Single(Build(), c => c.Id == id);

    [Fact]
    public void Defaults_MatchTheSpecTable()
    {
        var byId = Build().ToDictionary(c => c.Id);

        Assert.True(byId[CleanupCategoryId.TemporaryFiles].IsSelectedByDefault);
        Assert.False(byId[CleanupCategoryId.TemporaryFiles].RequiresAdmin);
        Assert.Equal(TimeSpan.FromHours(24), byId[CleanupCategoryId.TemporaryFiles].MinimumAge);

        Assert.True(byId[CleanupCategoryId.WindowsTemporaryFiles].RequiresAdmin);
        Assert.Equal(TimeSpan.FromHours(24), byId[CleanupCategoryId.WindowsTemporaryFiles].MinimumAge);

        Assert.False(byId[CleanupCategoryId.ThumbnailCache].IsSelectedByDefault);
        Assert.Equal("thumbcache_*.db", byId[CleanupCategoryId.ThumbnailCache].FileNamePattern);

        Assert.True(byId[CleanupCategoryId.WindowsUpdateLeftovers].RequiresAdmin);
        Assert.Equal(TimeSpan.FromDays(7), byId[CleanupCategoryId.WindowsUpdateLeftovers].MinimumAge);

        Assert.False(byId[CleanupCategoryId.DeliveryOptimizationCache].IsSelectedByDefault);
        Assert.True(byId[CleanupCategoryId.DeliveryOptimizationCache].RequiresAdmin);
    }

    [Fact]
    public void RecycleBin_IsOffByDefault_AndSaysPlainlyItIsPermanent()
    {
        var recycleBin = Get(CleanupCategoryId.RecycleBin);

        Assert.False(recycleBin.IsSelectedByDefault);
        Assert.True(recycleBin.IsRecycleBin);
        Assert.Contains("Permanently deletes everything in the Recycle Bin", recycleBin.Description);
    }

    [Fact]
    public void CrashReports_HasAnAdminOnlyRootButIsNotAdminOnlyOverall()
    {
        var crash = Get(CleanupCategoryId.CrashReports);

        Assert.False(crash.RequiresAdmin);
        Assert.Contains(crash.Roots, r => r.RequiresAdmin);
        Assert.Contains(crash.Roots, r => !r.RequiresAdmin);
    }

    [Fact]
    public void TemporaryFiles_UsesTheProvidedTempPath()
    {
        _paths.TempPath = @"C:\Fake\CustomTemp";

        Assert.Equal(@"C:\Fake\CustomTemp", Assert.Single(Get(CleanupCategoryId.TemporaryFiles).Roots).Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("temp")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Fake\Windows")]
    [InlineData(@"C:\Fake\Users\Test")]
    [InlineData(@"C:\Fake\Program Files")]
    public void DangerousTempPath_DropsTheCategory(string tempPath)
    {
        _paths.TempPath = tempPath;

        Assert.DoesNotContain(Build(), c => c.Id == CleanupCategoryId.TemporaryFiles);
    }

    [Fact]
    public void UnresolvedWindowsDirectory_DropsEveryWindowsCategory()
    {
        _paths.WindowsDirectory = string.Empty;

        var ids = Build().Select(c => c.Id).ToList();

        Assert.DoesNotContain(CleanupCategoryId.WindowsTemporaryFiles, ids);
        Assert.DoesNotContain(CleanupCategoryId.WindowsUpdateLeftovers, ids);
        Assert.DoesNotContain(CleanupCategoryId.DeliveryOptimizationCache, ids);
        Assert.Contains(CleanupCategoryId.TemporaryFiles, ids);
    }

    [Fact]
    public void BrowserCaches_OnlyListsTheNamedCacheFoldersOfExistingProfiles()
    {
        var userData = _paths.LocalAppData + @"\Google\Chrome\User Data";
        _fs.AddDirectory(userData)
            .AddDirectory(userData + @"\Default")
            .AddDirectory(userData + @"\Default\Cache")
            .AddDirectory(userData + @"\Default\GPUCache")
            .AddDirectory(userData + @"\Default\Cookies")
            .AddDirectory(userData + @"\Profile 1")
            .AddDirectory(userData + @"\Profile 1\Code Cache");

        var roots = Get(CleanupCategoryId.BrowserCaches).Roots;

        Assert.Equal(3, roots.Count);
        Assert.All(roots, root =>
        {
            Assert.Equal("chrome", root.BlockingProcessName);
            Assert.Equal("Google Chrome", root.BlockingProcessDisplayName);
        });
        Assert.DoesNotContain(roots, r => r.Path.Contains("Cookies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BrowserCaches_IncludesFirefoxCache2_AndIgnoresReparsePointProfiles()
    {
        var profiles = _paths.LocalAppData + @"\Mozilla\Firefox\Profiles";
        _fs.AddDirectory(profiles)
            .AddDirectory(profiles + @"\abc.default")
            .AddDirectory(profiles + @"\abc.default\cache2")
            .AddReparseDirectory(profiles + @"\linked")
            .AddDirectory(profiles + @"\linked\cache2");

        var root = Assert.Single(Get(CleanupCategoryId.BrowserCaches).Roots);

        Assert.EndsWith(@"abc.default\cache2", root.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("firefox", root.BlockingProcessName);
    }

    [Fact]
    public void BrowserCaches_NoBrowsersInstalled_IsLeftOut()
    {
        Assert.DoesNotContain(Build(), c => c.Id == CleanupCategoryId.BrowserCaches);
    }
}
