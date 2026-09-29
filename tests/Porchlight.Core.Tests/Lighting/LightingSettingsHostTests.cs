using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.Core.Tests.Lighting;

public sealed class LightingSettingsHostTests
{
    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("127.0.0.5", "127.0.0.5")]
    [InlineData("::1", "::1")]
    [InlineData("localhost", "localhost")]
    [InlineData("LocalHost", "LocalHost")]
    [InlineData(" 127.0.0.1 ", "127.0.0.1")]
    public void ResolveOpenRgbHost_LoopbackIsHonoured(string configured, string expected)
    {
        var settings = new LightingSettings { OpenRgbHost = configured };

        Assert.Equal(expected, settings.ResolveOpenRgbHost());
    }

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("10.0.0.1")]
    [InlineData("evil.example.com")]
    [InlineData("0.0.0.0")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveOpenRgbHost_NonLoopbackFallsBackToDefault(string? configured)
    {
        var settings = new LightingSettings { OpenRgbHost = configured! };

        Assert.Equal(LightingSettings.DefaultOpenRgbHost, settings.ResolveOpenRgbHost());
    }
}
