using Porchlight.Core.WindowsServices;
using Xunit;

namespace Porchlight.Core.Tests.WindowsServices;

public sealed class ServiceClassifierTests
{
    private const string Windows = @"C:\Windows";

    [Theory]
    [InlineData(@"C:\Program Files\Foo\foo.exe", "Microsoft Corporation", true)]
    [InlineData(@"C:\Program Files\Foo\foo.exe", "microsoft windows", true)]
    [InlineData(@"C:\Windows\System32\svchost.exe", null, true)]
    [InlineData(@"c:\windows\system32\svchost.exe", "Someone", true)]
    [InlineData(null, "Foo Inc", true)]
    [InlineData(@"C:\Program Files\Foo\foo.exe", "Foo Inc", false)]
    [InlineData(@"C:\Program Files\Foo\foo.exe", null, false)]
    [InlineData(@"C:\Windows Tools\foo.exe", "Foo Inc", false)]
    public void IsMicrosoft_ByCompanyWindowsFolderOrUnknown(string? path, string? company, bool expected)
    {
        Assert.Equal(expected, ServiceClassifier.IsMicrosoft(path, company, Windows));
    }

    [Theory]
    [InlineData("Kernel Driver", true)]
    [InlineData("File System Driver", true)]
    [InlineData("Own Process", false)]
    [InlineData("Share Process", false)]
    [InlineData(null, false)]
    public void IsDriver_DetectsDriverTypes(string? type, bool expected)
    {
        Assert.Equal(expected, ServiceClassifier.IsDriver(type));
    }

    [Theory]
    [InlineData("Auto", false, ServiceStartType.Automatic, "Starts with Windows")]
    [InlineData("Auto", true, ServiceStartType.AutomaticDelayed, "Starts with Windows (delayed)")]
    [InlineData("Manual", false, ServiceStartType.Manual, "Starts when needed")]
    [InlineData("Disabled", false, ServiceStartType.Disabled, "Turned off")]
    [InlineData("Boot", false, ServiceStartType.Automatic, "Starts with Windows")]
    [InlineData(null, false, ServiceStartType.Manual, "Starts when needed")]
    public void StartType_ParsedAndLabelledInPlainWords(string? mode, bool delayed, ServiceStartType expected, string label)
    {
        var type = ServiceClassifier.ParseStartType(mode, delayed);

        Assert.Equal(expected, type);
        Assert.Equal(label, ServiceClassifier.StartTypeLabel(type));
    }

    [Theory]
    [InlineData("Running", ServiceRunState.Running, "Running")]
    [InlineData("Stopped", ServiceRunState.Stopped, "Stopped")]
    [InlineData("Start Pending", ServiceRunState.Starting, "Starting...")]
    [InlineData("Stop Pending", ServiceRunState.Stopping, "Stopping...")]
    [InlineData("Paused", ServiceRunState.Other, "Paused")]
    public void State_ParsedAndLabelled(string state, ServiceRunState expected, string label)
    {
        var parsed = ServiceClassifier.ParseState(state);

        Assert.Equal(expected, parsed);
        Assert.Equal(label, ServiceClassifier.StateLabel(parsed));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("Keeps Foo up to date. Needs the network.", "Keeps Foo up to date.")]
    [InlineData("Keeps  Foo\r\nup to date", "Keeps Foo up to date")]
    [InlineData("Version 1.5 helper", "Version 1.5 helper")]
    public void ShortDescription_FirstSentence(string? description, string expected)
    {
        Assert.Equal(expected, ServiceClassifier.ShortDescription(description));
    }

    [Fact]
    public void ShortDescription_LongTextIsCut()
    {
        var result = ServiceClassifier.ShortDescription(new string('a', 400));

        Assert.True(result.Length <= 160);
        Assert.EndsWith("...", result, StringComparison.Ordinal);
    }
}
