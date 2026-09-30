using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public class InstallTypeDetectorTests
{
    private sealed class FakeAppInfo(string? directory) : IRunningAppInfo
    {
        public Version Version { get; } = new(0, 1, 0);

        public string? ExecutableDirectory { get; } = directory;
    }

    private sealed class FakeLocationReader(string? location) : IInstallLocationReader
    {
        public string? GetInstallLocation() => location;
    }

    private static InstallType Detect(string? running, string? installed) =>
        new InstallTypeDetector(new FakeAppInfo(running), new FakeLocationReader(installed)).Detect();

    [Fact]
    public void SameFolder_IsInstalled() =>
        Assert.Equal(InstallType.Installed, Detect(@"C:\Program Files\Porchlight", @"C:\Program Files\Porchlight"));

    [Fact]
    public void DifferentCasingAndTrailingSlash_IsStillInstalled() =>
        Assert.Equal(InstallType.Installed, Detect(@"c:\program files\porchlight", @"C:\Program Files\Porchlight\"));

    [Fact]
    public void ForwardSlashesAndDotSegments_AreNormalised() =>
        Assert.Equal(InstallType.Installed, Detect(@"C:\Program Files\Porchlight", "C:/Program Files/./Porchlight/"));

    [Fact]
    public void QuotedInstallLocation_IsNormalised() =>
        Assert.Equal(InstallType.Installed, Detect(@"C:\Program Files\Porchlight", "\"C:\\Program Files\\Porchlight\\\""));

    [Fact]
    public void OtherFolder_IsPortable() =>
        Assert.Equal(InstallType.Portable, Detect(@"D:\Downloads\Porchlight", @"C:\Program Files\Porchlight"));

    [Fact]
    public void SubfolderOfInstallLocation_IsPortable() =>
        Assert.Equal(InstallType.Portable, Detect(@"C:\Program Files\Porchlight\bin", @"C:\Program Files\Porchlight"));

    [Fact]
    public void NotInstalled_IsPortable() =>
        Assert.Equal(InstallType.Portable, Detect(@"C:\Program Files\Porchlight", null));

    [Fact]
    public void UnknownRunningFolder_IsPortable() =>
        Assert.Equal(InstallType.Portable, Detect(null, @"C:\Program Files\Porchlight"));

    [Fact]
    public void BlankInstallLocation_IsPortable() =>
        Assert.Equal(InstallType.Portable, Detect(@"C:\Program Files\Porchlight", "  "));
}
