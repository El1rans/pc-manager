using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests;

public sealed class AppDataPathsTests : IDisposable
{
    private const string AppData = @"C:\Users\someone\AppData\Roaming";
    private const string Temp = @"C:\Users\someone\AppData\Local\Temp";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "PorchlightAppDataPathsTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Func<string, string?> Env(string? dataDir = null, string? demo = null) => name => name switch
    {
        AppDataPaths.DataDirVariable => dataDir,
        AppDataPaths.DemoDataVariable => demo,
        _ => null,
    };

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "0")]
    [InlineData("   ", "true")]
    public void Resolve_NoOverride_UsesRealAppDataFolder(string? dataDir, string? demo)
    {
        var resolution = AppDataPaths.Resolve(Env(dataDir, demo), AppData, Temp, honourOverrides: true);

        Assert.Equal(new AppDataPaths.Resolution(Path.Combine(AppData, "Porchlight"), AppDataPaths.OverrideKind.None), resolution);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public void Resolve_DataDirSet_UsesItAsRoot_EvenInDemoMode(string? demo)
    {
        var resolution = AppDataPaths.Resolve(Env(@"D:\scratch\pl-data", demo), AppData, Temp, honourOverrides: true);

        Assert.Equal(new AppDataPaths.Resolution(@"D:\scratch\pl-data", AppDataPaths.OverrideKind.DataDir), resolution);
    }

    [Fact]
    public void Resolve_DemoDataWithoutDataDir_UsesTempDemoFolder()
    {
        var resolution = AppDataPaths.Resolve(Env(demo: "1"), AppData, Temp, honourOverrides: true);

        Assert.Equal(new AppDataPaths.Resolution(Path.Combine(Temp, "Porchlight-demo"), AppDataPaths.OverrideKind.DemoDefault), resolution);
    }

    [Theory]
    [InlineData(@"relative\folder")]
    [InlineData(@"\rooted-but-no-drive")]
    [InlineData(@"C:drive-relative")]
    public void Resolve_DataDirNotAbsolute_Throws_RatherThanFallingBackToRealFolder(string dataDir)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AppDataPaths.Resolve(Env(dataDir, "1"), AppData, Temp, honourOverrides: true));

        Assert.Contains(AppDataPaths.DataDirVariable, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"D:\scratch\pl-data", null)]
    [InlineData(null, "1")]
    [InlineData("relative", "1")]
    public void Resolve_OverridesNotHonoured_AlwaysUsesRealFolder(string? dataDir, string? demo)
    {
        var resolution = AppDataPaths.Resolve(Env(dataDir, demo), AppData, Temp, honourOverrides: false);

        Assert.Equal(new AppDataPaths.Resolution(Path.Combine(AppData, "Porchlight"), AppDataPaths.OverrideKind.None), resolution);
    }

#if !DEBUG
    [Fact]
    public void ReleaseBuild_RootIsAlwaysTheRealAppDataFolder()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Porchlight");

        Assert.False(AppDataPaths.IsOverridden);
        Assert.Equal(real, AppDataPaths.Root);
        Assert.Equal(Path.Combine(real, "settings.json"), AppDataPaths.SettingsFile);
    }
#endif

    [Fact]
    public void PrepareOverrideFolder_DemoDefault_SeedsSettingsWithFirstRunCompleted()
    {
        AppDataPaths.PrepareOverrideFolder(new AppDataPaths.Resolution(_root, AppDataPaths.OverrideKind.DemoDefault));

        var store = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_root, "settings.json"));
        Assert.True(store.Current.Setup.FirstRunCompleted);
    }

    [Fact]
    public void PrepareOverrideFolder_DemoDefault_NeverOverwritesExistingSettings()
    {
        Directory.CreateDirectory(_root);
        var settingsPath = Path.Combine(_root, "settings.json");
        File.WriteAllText(settingsPath, """{"Setup":{"FirstRunCompleted":false,"LaunchCount":7}}""");

        AppDataPaths.PrepareOverrideFolder(new AppDataPaths.Resolution(_root, AppDataPaths.OverrideKind.DemoDefault));

        Assert.Equal("""{"Setup":{"FirstRunCompleted":false,"LaunchCount":7}}""", File.ReadAllText(settingsPath));
    }

    [Fact]
    public void PrepareOverrideFolder_DataDir_CreatesFolderWithoutSeeding()
    {
        AppDataPaths.PrepareOverrideFolder(new AppDataPaths.Resolution(_root, AppDataPaths.OverrideKind.DataDir));

        Assert.True(Directory.Exists(_root));
        Assert.False(File.Exists(Path.Combine(_root, "settings.json")));
    }

    [Fact]
    public void PrepareOverrideFolder_NoOverride_TouchesNothing()
    {
        AppDataPaths.PrepareOverrideFolder(new AppDataPaths.Resolution(_root, AppDataPaths.OverrideKind.None));

        Assert.False(Directory.Exists(_root));
    }
}
