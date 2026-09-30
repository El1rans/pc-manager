using Microsoft.Win32;

namespace Porchlight.Core.SelfUpdate;

/// <summary>Reads where the Setup installer put Porchlight, behind an interface so install-type
/// detection is unit-testable without the registry. (A separate slice from
/// <c>Components.IRegistryReader</c>, which is about third-party components, so that interface and
/// its fakes stay untouched.)</summary>
public interface IInstallLocationReader
{
    /// <summary>The <c>InstallLocation</c> recorded by the installer, or null if Porchlight isn't installed.</summary>
    string? GetInstallLocation();
}

/// <inheritdoc cref="IInstallLocationReader"/>
public sealed class RegistryInstallLocationReader : IInstallLocationReader
{
    /// <summary>Uninstall key of the installer's fixed <c>AppId</c> plus Inno Setup's <c>_is1</c> suffix
    /// (<c>installer/Porchlight.iss</c>, <c>MyAppId</c>).</summary>
    public const string UninstallKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{A9E642BD-1DA1-421E-81D3-4416ED5C45F3}_is1";

    public string? GetInstallLocation()
    {
        try
        {
            // The installer installs in 64-bit mode, so its key is in the 64-bit registry view.
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(UninstallKeyPath);
            return key?.GetValue("InstallLocation") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
