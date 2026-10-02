using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.Core.Tests.WindowsServices;

public sealed class ServiceImagePathParserTests
{
    private const string Windows = @"C:\Windows";

    [Theory]
    [InlineData(@"""C:\Program Files\Foo\foo.exe"" -service", @"C:\Program Files\Foo\foo.exe")]
    [InlineData(@"C:\Program Files\Foo\foo.exe -service", @"C:\Program Files\Foo\foo.exe")]
    [InlineData(@"C:\Foo\foo.exe", @"C:\Foo\foo.exe")]
    [InlineData(@"\??\C:\Foo\foo.exe /x", @"C:\Foo\foo.exe")]
    [InlineData(@"\SystemRoot\System32\drivers\foo.exe", @"C:\Windows\System32\drivers\foo.exe")]
    [InlineData(@"system32\svchost.exe -k netsvcs", @"C:\Windows\system32\svchost.exe")]
    [InlineData(@"C:\WINDOWS\system32\svchost.exe -k netsvcs -p", @"C:\WINDOWS\system32\svchost.exe")]
    public void ExtractExecutablePath_FindsProgram(string pathName, string expected)
    {
        Assert.Equal(expected, ServiceImagePathParser.ExtractExecutablePath(pathName, Windows));
    }

    [Fact]
    public void ExtractExecutablePath_ExpandsEnvironmentVariables()
    {
        var expected = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "foo.exe");

        Assert.Equal(expected, ServiceImagePathParser.ExtractExecutablePath(@"%SystemRoot%\foo.exe -x", Windows));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractExecutablePath_EmptyIsNull(string? pathName)
    {
        Assert.Null(ServiceImagePathParser.ExtractExecutablePath(pathName, Windows));
    }
}
