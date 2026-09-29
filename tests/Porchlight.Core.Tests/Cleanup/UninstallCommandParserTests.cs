using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class UninstallCommandParserTests
{
    [Fact]
    public void Parse_QuotedPathWithArgument()
    {
        var command = UninstallCommandParser.Parse(@"""C:\Program Files\App\unins000.exe"" /SILENTX");

        Assert.NotNull(command);
        Assert.Equal(@"C:\Program Files\App\unins000.exe", command.FileName);
        Assert.Equal(["/SILENTX"], command.Arguments);
    }

    [Fact]
    public void Parse_QuotedPathWithNoArguments()
    {
        var command = UninstallCommandParser.Parse(@"""C:\App\uninstall.exe""");

        Assert.NotNull(command);
        Assert.Equal(@"C:\App\uninstall.exe", command.FileName);
        Assert.Empty(command.Arguments);
    }

    [Fact]
    public void Parse_MsiExecWithProductCode()
    {
        var command = UninstallCommandParser.Parse("MsiExec.exe /X{0A1B2C3D-1111-2222-3333-444455556666}");

        Assert.NotNull(command);
        Assert.Equal("MsiExec.exe", command.FileName);
        Assert.Equal(["/X{0A1B2C3D-1111-2222-3333-444455556666}"], command.Arguments);
    }

    [Fact]
    public void Parse_UnquotedPathContainingSpaces_KeepsTheProgramWhole()
    {
        var command = UninstallCommandParser.Parse(@"C:\Program Files\Some App\uninstall.exe /allusers");

        Assert.NotNull(command);
        Assert.Equal(@"C:\Program Files\Some App\uninstall.exe", command.FileName);
        Assert.Equal(["/allusers"], command.Arguments);
    }

    [Fact]
    public void Parse_QuotedArgumentsWithSpaces_StayTogether()
    {
        var command = UninstallCommandParser.Parse(@"""C:\App\u.exe"" /D=""C:\My Files\App"" -x");

        Assert.NotNull(command);
        Assert.Equal(["/D=C:\\My Files\\App", "-x"], command.Arguments);
    }

    [Fact]
    public void Parse_UnquotedPathWithQuotedArguments()
    {
        var command = UninstallCommandParser.Parse(@"C:\App\uninst.exe /p ""two words""");

        Assert.NotNull(command);
        Assert.Equal(@"C:\App\uninst.exe", command.FileName);
        Assert.Equal(["/p", "two words"], command.Arguments);
    }

    [Fact]
    public void Parse_NoExeExtension_FallsBackToPlainCommandLineRules()
    {
        var command = UninstallCommandParser.Parse("rundll32 shell32.dll,Control_RunDLL appwiz.cpl");

        Assert.NotNull(command);
        Assert.Equal("rundll32", command.FileName);
        Assert.Equal(["shell32.dll,Control_RunDLL", "appwiz.cpl"], command.Arguments);
    }

    [Fact]
    public void Parse_ExeAppearingInsideAFolderName_IsNotTheProgramEnd()
    {
        var command = UninstallCommandParser.Parse(@"C:\tools.exe.d\real.exe /u");

        Assert.NotNull(command);
        Assert.Equal(@"C:\tools.exe.d\real.exe", command.FileName);
        Assert.Equal(["/u"], command.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_IsNull(string? commandLine)
    {
        Assert.Null(UninstallCommandParser.Parse(commandLine));
    }
}
