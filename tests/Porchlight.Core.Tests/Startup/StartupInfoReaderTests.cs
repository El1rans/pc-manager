using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupInfoReaderTests : IDisposable
{
    private const string Sid = "S-1-5-21-TEST";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "porchlight-startupinfo-" + Guid.NewGuid().ToString("N"));

    public StartupInfoReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private StartupInfoReader Create(string? sid = Sid) => new(NullLogger<StartupInfoReader>.Instance, _folder, sid);

    private void Write(string name, string cpuMs, DateTime writtenUtc)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, $"<r><p ImagePath=\"C:\\a.exe\" CpuTimeMs=\"{cpuMs}\"/></r>");
        File.SetLastWriteTimeUtc(path, writtenUtc);
    }

    [Fact]
    public void Read_UsesNewestFileOfTheCurrentUserOnly()
    {
        Write($"{Sid}_StartupInfo1.xml", "100", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Write($"{Sid}_StartupInfo2.xml", "200", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        Write("S-1-5-OTHER_StartupInfo3.xml", "999", new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

        var result = Create().Read();

        Assert.False(result.AccessDenied);
        Assert.Equal(200, Assert.Single(result.Records).CpuTimeMs);
    }

    [Fact]
    public void Read_NoFileOrNoFolderOrNoSid_IsEmptyNotDenied()
    {
        Assert.Empty(Create().Read().Records);
        Assert.Empty(Create(sid: null).Read().Records);
        Assert.Empty(new StartupInfoReader(NullLogger<StartupInfoReader>.Instance, Path.Combine(_folder, "missing"), Sid).Read().Records);
        Assert.False(Create().Read().AccessDenied);
    }

    [Fact]
    public void Read_CorruptFile_IsEmptyNotThrown()
    {
        File.WriteAllText(Path.Combine(_folder, $"{Sid}_StartupInfo1.xml"), "garbage");

        Assert.Empty(Create().Read().Records);
    }
}
