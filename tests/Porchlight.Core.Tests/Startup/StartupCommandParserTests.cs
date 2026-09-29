using Porchlight.Core.Startup;
using Xunit;

namespace Porchlight.Core.Tests.Startup;

public sealed class StartupCommandParserTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\App\app.exe"" --minimized", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe /background", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Tools\tool.EXE", @"C:\Tools\tool.EXE")]
    [InlineData(@"C:\Tools\tool -x", @"C:\Tools\tool")]
    [InlineData(@"""C:\Tools\tool.exe""", @"C:\Tools\tool.exe")]
    public void ExtractExecutablePath_FindsProgram(string command, string expected)
    {
        Assert.Equal(expected, StartupCommandParser.ExtractExecutablePath(command));
    }

    [Fact]
    public void ExtractExecutablePath_ExpandsEnvironmentVariables()
    {
        var expected = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, "notepad.exe");

        Assert.Equal(expected, StartupCommandParser.ExtractExecutablePath(@"%SystemRoot%\notepad.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void ExtractExecutablePath_EmptyIsNull(string? command)
    {
        Assert.Null(StartupCommandParser.ExtractExecutablePath(command));
    }
}
