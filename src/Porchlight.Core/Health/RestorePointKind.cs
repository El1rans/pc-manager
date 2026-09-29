namespace Porchlight.Core.Health;

/// <summary>The <c>RestorePointType</c> passed to <c>SystemRestore.CreateRestorePoint</c>.</summary>
public enum RestorePointKind
{
    /// <summary>APPLICATION_INSTALL.</summary>
    ApplicationInstall = 0,

    /// <summary>MODIFY_SETTINGS.</summary>
    ModifySettings = 12,
}
