using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupInfoParserTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Startup", "Fixtures", "SampleStartupInfo.xml"));

    [Fact]
    public void Parse_Fixture_ReadsChildElementAndAttributeRecords()
    {
        var records = StartupInfoParser.Parse(Fixture());

        Assert.Equal(3, records.Count);
        var chat = records.Single(r => r.ImagePath.EndsWith("chat.exe", StringComparison.Ordinal));
        Assert.Equal(1450, chat.CpuTimeMs);
        Assert.Equal(524288, chat.DiskBytes);
        var tiny = records.Single(r => r.ImagePath.EndsWith("tiny.exe", StringComparison.Ordinal));
        Assert.Equal((12, 2048), (tiny.CpuTimeMs, tiny.DiskBytes));
    }

    [Fact]
    public void Parse_TaskManagerShape_ReadsTheNameAttributeNotTheParent()
    {
        const string xml = """
            <StartupData>
              <Process Name="C:\Program Files\Vendor\sync.exe" PID="4242" StartTime="2026-10-01T08:00:00Z">
                <ParentPID>1200</ParentPID>
                <ParentName>C:\Windows\explorer.exe</ParentName>
                <DiskUsage>4194304</DiskUsage>
                <CpuUsage>250</CpuUsage>
              </Process>
            </StartupData>
            """;

        var record = Assert.Single(StartupInfoParser.Parse(xml));

        Assert.Equal(@"C:\Program Files\Vendor\sync.exe", record.ImagePath);
        Assert.Equal((250, 4194304), (record.CpuTimeMs, record.DiskBytes));
    }

    [Fact]
    public void Parse_FixtureStream_GivesSameResultAsString()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Startup", "Fixtures", "SampleStartupInfo.xml"));

        Assert.Equal(3, StartupInfoParser.Parse(stream).Count);
    }

    [Fact]
    public void Parse_MissingMetric_DefaultsItToZero()
    {
        var records = StartupInfoParser.Parse("<r><p ImagePath=\"C:\\a.exe\" CpuTimeMs=\"500\"/></r>");

        var record = Assert.Single(records);
        Assert.Equal((500, 0), (record.CpuTimeMs, record.DiskBytes));
    }

    [Fact]
    public void Parse_HonoursUnitHints()
    {
        const string xml = "<r><p Image=\"C:\\a.exe\" CpuSec=\"2\" DiskKB=\"512\"/><p Image=\"C:\\b.exe\" Cpu=\"1.5 s\" DiskUsage=\"2 MB\"/></r>";

        var records = StartupInfoParser.Parse(xml);

        Assert.Equal((2000, 512L * 1024), (records[0].CpuTimeMs, records[0].DiskBytes));
        Assert.Equal((1500, 2L * 1024 * 1024), (records[1].CpuTimeMs, records[1].DiskBytes));
    }

    [Fact]
    public void Parse_CommandLineField_ExtractsTheExecutable()
    {
        var records = StartupInfoParser.Parse("<r><p CommandLine='\"C:\\Program Files\\a.exe\" --x' CpuTimeMs=\"5\"/></r>");

        Assert.Equal(@"C:\Program Files\a.exe", Assert.Single(records).ImagePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not xml at all")]
    [InlineData("<a><b></a>")]
    [InlineData("<StartupInfo/>")]
    [InlineData("<r><p ImagePath=\"C:\\a.exe\" CpuTimeMs=\"abc\"/></r>")]
    [InlineData("<r><p ImagePath=\"C:\\a.exe\" CpuTimeMs=\"-5\"/></r>")]
    public void Parse_GarbageOrEmpty_GivesNoRecords(string xml)
    {
        Assert.Empty(StartupInfoParser.Parse(xml));
    }

    [Fact]
    public void Parse_DtdIsRefused_GivesNoRecords()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE r [<!ENTITY x \"y\">]><r><p ImagePath=\"C:\\a.exe\" CpuTimeMs=\"5\"/></r>";

        Assert.Empty(StartupInfoParser.Parse(xml));
    }

    [Fact]
    public void Parse_GarbageStream_GivesNoRecords()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        Assert.Empty(StartupInfoParser.Parse(stream));
    }
}
