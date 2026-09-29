using System.Runtime.InteropServices;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Checks a file's Authenticode signature, behind an interface so the downloader is
/// testable. Only consulted when <see cref="SelfUpdatePolicy.RequireSignedInstaller"/> is on.</summary>
public interface IInstallerSignatureVerifier
{
    /// <summary>True if <paramref name="path"/> has an embedded Authenticode signature that Windows
    /// considers valid and trusted.</summary>
    bool HasValidSignature(string path);
}

/// <inheritdoc cref="IInstallerSignatureVerifier"/>
public sealed class WinTrustSignatureVerifier : IInstallerSignatureVerifier
{
    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionIgnore = 0;
    // No revocation lookup: keeps the check offline (no extra network contact); the hash check is
    // what protects against tampering, the signature adds who published it.
    private const uint WtdRevocationCheckNone = 0x00000010;

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public bool HasValidSignature(string path)
    {
        var pathPointer = Marshal.StringToHGlobalUni(path);
        var filePointer = IntPtr.Zero;
        try
        {
            var fileInfo = new WinTrustFileInfo
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                FilePath = pathPointer,
            };
            filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(fileInfo, filePointer, fDeleteOld: false);

            var data = new WinTrustData
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                UiChoice = WtdUiNone,
                RevocationChecks = WtdRevokeNone,
                UnionChoice = WtdChoiceFile,
                File = filePointer,
                StateAction = WtdStateActionIgnore,
                ProvFlags = WtdRevocationCheckNone,
            };

            var action = GenericVerifyV2;
            return WinVerifyTrust(new IntPtr(-1), ref action, ref data) == 0;
        }
        finally
        {
            if (filePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(filePointer);
            }

            Marshal.FreeHGlobal(pathPointer);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref WinTrustData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
