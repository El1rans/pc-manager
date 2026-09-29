using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupClassifierTests
{
    private const string WindowsDir = @"C:\Windows";

    [Fact]
    public void Classify_MicrosoftPublisher_IsRecommendedToKeep()
    {
        var result = StartupClassifier.Classify("SecurityHealth", @"C:\Program Files\X\x.exe", "Microsoft Corporation", null, WindowsDir);

        Assert.True(result.RecommendedToKeep);
    }

    [Fact]
    public void Classify_PathInsideWindowsFolder_IsRecommendedToKeep()
    {
        var result = StartupClassifier.Classify("Thing", @"C:\Windows\System32\thing.exe", null, null, WindowsDir);

        Assert.True(result.RecommendedToKeep);
    }

    [Fact]
    public void Classify_SiblingFolderOfWindows_IsNotTreatedAsWindows()
    {
        var result = StartupClassifier.Classify("Thing", @"C:\Windows.old\thing.exe", null, null, WindowsDir);

        Assert.False(result.RecommendedToKeep);
    }

    [Theory]
    [InlineData("AnyDesk", @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe")]
    [InlineData("OpenRGB", @"C:\Tools\OpenRGB\OpenRGB.exe")]
    [InlineData("Porchlight", @"C:\Program Files\Porchlight\Porchlight.exe")]
    public void Classify_PorchlightComponents_AreRecommendedToKeep(string name, string path)
    {
        var result = StartupClassifier.Classify(name, path, "Someone Else", null, WindowsDir);

        Assert.True(result.RecommendedToKeep);
    }

    [Fact]
    public void Classify_OneDrive_HasSpecificHintAndIsNotRecommendedToKeep()
    {
        var result = StartupClassifier.Classify("OneDrive", @"C:\Users\a\OneDrive.exe", "Microsoft Corporation", null, WindowsDir);

        Assert.False(result.RecommendedToKeep);
        Assert.Contains("OneDrive", result.Hint);
    }

    [Fact]
    public void Classify_UnknownProgramWithDescription_UsesDescriptionThenGenericSentence()
    {
        var result = StartupClassifier.Classify("Foo", @"C:\Foo\foo.exe", "Foo Inc", "Foo photo helper", WindowsDir);

        Assert.False(result.RecommendedToKeep);
        Assert.StartsWith("Foo photo helper.", result.Hint);
        Assert.Contains("doesn't remove the program", result.Hint);
    }

    [Fact]
    public void Classify_UnknownProgramWithoutDescription_UsesGenericSentence()
    {
        var result = StartupClassifier.Classify("Foo", null, null, null, WindowsDir);

        Assert.StartsWith("Starts by itself", result.Hint);
    }
}
