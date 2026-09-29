namespace Porchlight.Core.SelfUpdate;

/// <summary>Security switches for the self-updater, in one place.</summary>
public static class SelfUpdatePolicy
{
    /// <summary>
    /// Whether a downloaded installer must carry a valid Authenticode signature before it is run, on
    /// top of the SHA-256 check. False today because releases are not code-signed yet (SignPath is not
    /// configured - see <c>docs/CODE_SIGNING_POLICY.md</c>). Once releases are always signed, flipping
    /// this to <c>true</c> is the whole change: <see cref="UpdateDownloader"/> then refuses any
    /// installer <see cref="IInstallerSignatureVerifier"/> doesn't accept. Don't flip it before the
    /// first signed release is published, or every update would fail.
    /// </summary>
    public const bool RequireSignedInstaller = false;
}
