namespace Porchlight.Core.Backup;

/// <summary>What OneDrive reports. Deliberately has no account email or folder path: neither is
/// ever needed on screen or in a report.</summary>
/// <param name="IsInstalled">OneDrive is installed for this user.</param>
/// <param name="IsSignedIn">At least one account is signed in.</param>
/// <param name="DesktopProtected">Desktop lives inside a signed-in OneDrive folder.</param>
/// <param name="DocumentsProtected">Documents lives inside a signed-in OneDrive folder.</param>
/// <param name="PicturesProtected">Pictures lives inside a signed-in OneDrive folder.</param>
/// <param name="ExePath">Full path of OneDrive.exe when it exists, used only to open it.</param>
public sealed record OneDriveStatus(
    bool IsInstalled,
    bool IsSignedIn,
    bool DesktopProtected,
    bool DocumentsProtected,
    bool PicturesProtected,
    string? ExePath)
{
    /// <summary>Documents and Desktop - the folders that matter most - are both protected.</summary>
    public bool CoversDocumentsAndDesktop => IsSignedIn && DocumentsProtected && DesktopProtected;

    /// <summary>At least one of the main folders is protected.</summary>
    public bool CoversAnything => IsSignedIn && (DocumentsProtected || DesktopProtected || PicturesProtected);

    public static OneDriveStatus NotInstalled { get; } = new(false, false, false, false, false, null);
}
