using Microsoft.Win32;

namespace Porchlight.Core.Backup;

/// <inheritdoc cref="IOneDriveReader"/>
public sealed class OneDriveReader : IOneDriveReader
{
    private const string AccountsKeyPath = @"Software\Microsoft\OneDrive\Accounts";
    private const string UserFolderValue = "UserFolder";
    private const string UserEmailValue = "UserEmail";
    private const string ShellFoldersKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders";
    private const string DesktopValue = "Desktop";
    private const string DocumentsValue = "Personal";
    private const string PicturesValue = "My Pictures";
    private const string OneDriveExeRelativePath = @"Microsoft\OneDrive\OneDrive.exe";

    public OneDriveStatus Read()
    {
        var exe = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OneDriveExeRelativePath);
        var exePath = File.Exists(exe) ? exe : null;

        var roots = new List<string>();
        var hasAccountKey = false;
        using (var accounts = Registry.CurrentUser.OpenSubKey(AccountsKeyPath))
        {
            foreach (var name in accounts?.GetSubKeyNames() ?? [])
            {
                hasAccountKey = true;
                using var account = accounts!.OpenSubKey(name);
                // The email is only tested for presence (a signed-in account has one); it is never kept.
                var signedIn = account?.GetValue(UserEmailValue) is string { Length: > 0 };
                if (signedIn && account!.GetValue(UserFolderValue) is string { Length: > 0 } folder)
                {
                    roots.Add(folder);
                }
            }
        }

        if (exePath is null && !hasAccountKey)
        {
            return OneDriveStatus.NotInstalled;
        }

        using var shell = Registry.CurrentUser.OpenSubKey(ShellFoldersKeyPath);
        return new OneDriveStatus(
            IsInstalled: true,
            IsSignedIn: roots.Count > 0,
            DesktopProtected: KnownFolderCoverage.IsInside(shell?.GetValue(DesktopValue) as string, roots),
            DocumentsProtected: KnownFolderCoverage.IsInside(shell?.GetValue(DocumentsValue) as string, roots),
            PicturesProtected: KnownFolderCoverage.IsInside(shell?.GetValue(PicturesValue) as string, roots),
            ExePath: exePath);
    }
}
