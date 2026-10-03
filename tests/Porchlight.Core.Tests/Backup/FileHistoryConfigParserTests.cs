using Porchlight.Core.Backup;
using Xunit;

namespace Porchlight.Core.Tests.Backup;

public sealed class FileHistoryConfigParserTests
{
    [Fact]
    public void TargetWithoutFlag_IsConfiguredAndOn()
    {
        var result = FileHistoryConfigParser.Parse(
            "<DataProtectionUserConfig><FileHistoryConfig><TargetUrl>E:\\</TargetUrl></FileHistoryConfig></DataProtectionUserConfig>");

        Assert.Equal((true, true), result);
    }

    [Theory]
    [InlineData("<Root><TargetName>Backup drive</TargetName><UserEnabled>false</UserEnabled></Root>")]
    [InlineData("<Root><TargetUrl>E:\\</TargetUrl><Enabled>FALSE</Enabled></Root>")]
    public void TargetWithOffFlag_IsConfiguredButOff(string xml) =>
        Assert.Equal((true, false), FileHistoryConfigParser.Parse(xml));

    [Fact]
    public void TargetWithOnFlag_IsOn() =>
        Assert.Equal((true, true), FileHistoryConfigParser.Parse("<Root><TargetUrl>E:\\</TargetUrl><UserEnabled>true</UserEnabled></Root>"));

    [Fact]
    public void EmptyTarget_IsNotConfigured() =>
        Assert.Equal((false, false), FileHistoryConfigParser.Parse("<Root><TargetUrl>  </TargetUrl></Root>"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not xml <")]
    public void Unreadable_ReturnsNull(string xml) => Assert.Null(FileHistoryConfigParser.Parse(xml));
}
