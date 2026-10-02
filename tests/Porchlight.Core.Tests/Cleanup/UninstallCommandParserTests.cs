using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class UninstallCommandParserTests
{
    public static TheoryData<string, string, string[]> CommandLines => new()
    {
        // Quoted path with an argument.
        { @"""C:\Program Files\App\unins000.exe"" /SILENTX", @"C:\Program Files\App\unins000.exe", ["/SILENTX"] },
        // Quoted path with no arguments.
        { @"""C:\App\uninstall.exe""", @"C:\App\uninstall.exe", [] },
        // MsiExec with a product code.
        { "MsiExec.exe /X{0A1B2C3D-1111-2222-3333-444455556666}", "MsiExec.exe", ["/X{0A1B2C3D-1111-2222-3333-444455556666}"] },
        // An unquoted path containing spaces keeps the program whole.
        { @"C:\Program Files\Some App\uninstall.exe /allusers", @"C:\Program Files\Some App\uninstall.exe", ["/allusers"] },
        // Quoted arguments with spaces stay together.
        { @"""C:\App\u.exe"" /D=""C:\My Files\App"" -x", @"C:\App\u.exe", ["/D=C:\\My Files\\App", "-x"] },
        // Unquoted path with quoted arguments.
        { @"C:\App\uninst.exe /p ""two words""", @"C:\App\uninst.exe", ["/p", "two words"] },
        // No .exe extension: falls back to plain command-line rules.
        { "rundll32 shell32.dll,Control_RunDLL appwiz.cpl", "rundll32", ["shell32.dll,Control_RunDLL", "appwiz.cpl"] },
        // ".exe" inside a folder name is not the end of the program.
        { @"C:\tools.exe.d\real.exe /u", @"C:\tools.exe.d\real.exe", ["/u"] },
    };

    [Theory]
    [MemberData(nameof(CommandLines))]
    public void Parse_SplitsTheCommandLineIntoProgramAndArguments(string commandLine, string expectedFileName, string[] expectedArguments)
    {
        var command = UninstallCommandParser.Parse(commandLine);

        Assert.NotNull(command);
        Assert.Equal(expectedFileName, command.FileName);
        Assert.Equal(expectedArguments, command.Arguments);
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
