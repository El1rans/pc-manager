namespace Porchlight.Core.RemoveApps;

/// <summary>How the "Remove apps" page treats an installed program.</summary>
public enum RemovableAppKind
{
    /// <summary>An ordinary program: listed with a Remove button.</summary>
    Normal,

    /// <summary>A runtime or driver the PC usually needs: listed in the collapsed "System parts" group.</summary>
    SystemPart,

    /// <summary>A component Porchlight installs and manages itself (AnyDesk, OpenRGB, PawnIO): listed
    /// as "Managed by Porchlight", never removable here.</summary>
    ManagedByPorchlight,

    /// <summary>Porchlight itself: never listed.</summary>
    Porchlight,
}
