using Porchlight.Core.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Hardware;

/// <summary>Fans polish addendum: <see cref="FanNaming.ResolveDisplayName"/> is the single fallback
/// rule every place a fan name is shown (Fans tab, Sensors tab RPM rows, "Hottest fan" tile) goes
/// through - proven directly here rather than through each of those view models.</summary>
public sealed class FanNamingTests
{
    [Fact]
    public void ResolveDisplayName_NoCustomNameSet_FallsBackToHardwareName()
    {
        var customNames = new Dictionary<string, string>();

        var resolved = FanNaming.ResolveDisplayName("fan-1", "CPU fan", customNames);

        Assert.Equal("CPU fan", resolved);
    }

    [Fact]
    public void ResolveDisplayName_CustomNameSet_ReturnsCustomName()
    {
        var customNames = new Dictionary<string, string> { ["fan-1"] = "Front intake" };

        var resolved = FanNaming.ResolveDisplayName("fan-1", "CPU fan", customNames);

        Assert.Equal("Front intake", resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveDisplayName_CustomNameBlank_FallsBackToHardwareName(string blank)
    {
        var customNames = new Dictionary<string, string> { ["fan-1"] = blank };

        var resolved = FanNaming.ResolveDisplayName("fan-1", "CPU fan", customNames);

        Assert.Equal("CPU fan", resolved);
    }

    [Fact]
    public void ResolveDisplayName_CustomNameSetForDifferentFan_IsNotUsed()
    {
        var customNames = new Dictionary<string, string> { ["fan-2"] = "Rear exhaust" };

        var resolved = FanNaming.ResolveDisplayName("fan-1", "CPU fan", customNames);

        Assert.Equal("CPU fan", resolved);
    }
}
