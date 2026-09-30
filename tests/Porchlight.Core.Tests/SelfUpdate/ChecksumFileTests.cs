using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public class ChecksumFileTests
{
    private const string Name = "Porchlight-Setup-0.2.0.exe";
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void TryParse_ReleaseWorkflowFormat_Works()
    {
        // Exactly what release.yml writes: lower-case hash, space, star, name, no trailing newline.
        Assert.True(ChecksumFile.TryParse($"{Hash} *{Name}", Name, out var hash));
        Assert.Equal(Hash, hash);
    }

    [Fact]
    public void TryParse_UpperCaseHex_IsNormalisedToLowerCase()
    {
        Assert.True(ChecksumFile.TryParse($"{Hash.ToUpperInvariant()}  {Name}\r\n", Name, out var hash));
        Assert.Equal(Hash, hash);
    }

    [Fact]
    public void TryParse_HashOnly_Works() =>
        Assert.True(ChecksumFile.TryParse(Hash, Name, out _));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a hash")]
    [InlineData("0123456789abcdef *Porchlight-Setup-0.2.0.exe")]
    [InlineData("zz23456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef *Porchlight-Setup-0.2.0.exe")]
    public void TryParse_BadFormat_Rejected(string content) =>
        Assert.False(ChecksumFile.TryParse(content, Name, out _));

    [Fact]
    public void TryParse_ChecksumForAnotherFile_Rejected() =>
        Assert.False(ChecksumFile.TryParse($"{Hash} *Something-Else.exe", Name, out _));

    [Fact]
    public void TryParse_MultipleLines_Rejected() =>
        Assert.False(ChecksumFile.TryParse($"{Hash} *{Name}\n{Hash} *{Name}", Name, out _));

    [Fact]
    public void TryParse_Null_Rejected() =>
        Assert.False(ChecksumFile.TryParse(null, Name, out _));
}
