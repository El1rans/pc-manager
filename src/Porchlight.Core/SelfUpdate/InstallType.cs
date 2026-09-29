namespace Porchlight.Core.SelfUpdate;

/// <summary>How the running Porchlight was put on this PC.</summary>
public enum InstallType
{
    /// <summary>Running from the folder the Setup installer installed to - can update itself.</summary>
    Installed,

    /// <summary>Anywhere else (unzipped copy, build output) - only points to the download page.</summary>
    Portable,
}
