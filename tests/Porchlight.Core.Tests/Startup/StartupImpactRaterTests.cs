using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupImpactRaterTests
{
    private const long Kb = 1024;
    private const long Mb = 1024 * 1024;

    [Theory]
    [InlineData(0, 0, StartupImpact.Low)]
    [InlineData(299, 300 * Kb - 1, StartupImpact.Low)]
    [InlineData(300, 0, StartupImpact.Medium)]
    [InlineData(0, 300 * Kb, StartupImpact.Medium)]
    [InlineData(1000, 3 * Mb, StartupImpact.Medium)]
    [InlineData(1001, 0, StartupImpact.High)]
    [InlineData(0, 3 * Mb + 1, StartupImpact.High)]
    [InlineData(50, 3 * Mb + 1, StartupImpact.High)]
    [InlineData(1001, 10, StartupImpact.High)]
    [InlineData(299, 300 * Kb, StartupImpact.Medium)]
    public void Classify_UsesTaskManagerThresholds_EitherMetricCanRaiseIt(double cpuMs, long diskBytes, StartupImpact expected)
    {
        Assert.Equal(expected, StartupImpactRater.Classify(cpuMs, diskBytes));
    }

    [Fact]
    public void Rate_NoRecord_IsNotMeasured()
    {
        var rater = new StartupImpactRater([new StartupInfoRecord(@"C:\a\a.exe", 5000, 0)]);

        Assert.Equal(StartupImpact.NotMeasured, rater.Rate(@"C:\b\b.exe"));
        Assert.Equal(StartupImpact.NotMeasured, rater.Rate(null));
        Assert.Equal(StartupImpact.NotMeasured, rater.Rate("  "));
    }

    [Theory]
    [InlineData(@"c:\PROGRAM FILES\Foo\FOO.EXE")]
    [InlineData(@"C:/Program Files/Foo/foo.exe")]
    [InlineData(@"\\?\C:\Program Files\Foo\foo.exe")]
    [InlineData(@"\??\C:\Program Files\Foo\foo.exe")]
    [InlineData("\"C:\\Program Files\\Foo\\foo.exe\"")]
    public void Rate_MatchesPathsIgnoringCaseAndPrefixes(string entryPath)
    {
        var rater = new StartupImpactRater([new StartupInfoRecord(@"C:\Program Files\Foo\foo.exe", 2000, 0)]);

        Assert.Equal(StartupImpact.High, rater.Rate(entryPath));
    }

    [Fact]
    public void Rate_DevicePathInTrace_MatchesDriveLetterPath()
    {
        var rater = new StartupImpactRater([new StartupInfoRecord(@"\Device\HarddiskVolume3\Tools\foo.exe", 400, 0)]);

        Assert.Equal(StartupImpact.Medium, rater.Rate(@"D:\Tools\foo.exe"));
    }

    [Fact]
    public void Rate_ExpandsEnvironmentVariables()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var rater = new StartupImpactRater([new StartupInfoRecord(Path.Combine(windows, "System32", "x.exe"), 10, 10)]);

        Assert.Equal(StartupImpact.Low, rater.Rate(@"%SystemRoot%\System32\x.exe"));
    }

    [Fact]
    public void Rate_SeveralRecordsForOnePath_UsesTheLargestOfEachMetric()
    {
        var rater = new StartupImpactRater(
        [
            new StartupInfoRecord(@"C:\a.exe", 400, 0),
            new StartupInfoRecord(@"C:\a.exe", 10, 4 * Mb),
        ]);

        Assert.Equal(StartupImpact.High, rater.Rate(@"C:\a.exe"));
    }
}
