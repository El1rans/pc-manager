using Porchlight.Core.SelfUpdate;
using Xunit;

namespace Porchlight.Core.Tests.SelfUpdate;

public sealed class WinTrustSignatureVerifierTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "PorchlightSig_" + Guid.NewGuid().ToString("N") + ".exe");

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    [Fact]
    public void UnsignedFile_IsNotValid()
    {
        File.WriteAllBytes(_file, [0x4D, 0x5A, 0x00, 0x00]);

        Assert.False(new WinTrustSignatureVerifier().HasValidSignature(_file));
    }

    [Fact]
    public void MissingFile_IsNotValid() =>
        Assert.False(new WinTrustSignatureVerifier().HasValidSignature(_file));

    [Fact]
    public void EmbeddedSignedMicrosoftBinary_IsValid()
    {
        // The dotnet host carries an embedded Microsoft Authenticode signature wherever the SDK is installed.
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        if (!File.Exists(dotnet))
        {
            return;
        }

        Assert.True(new WinTrustSignatureVerifier().HasValidSignature(dotnet));
    }
}
