namespace Porchlight.Core.Components;

/// <summary>The fixed set of components milestone 01b knows about. Verified with
/// <c>winget show --id &lt;id&gt; --exact</c> during development (see 01b-components.md).</summary>
public static class ComponentCatalog
{
    public static IReadOnlyList<ComponentDefinition> All { get; } =
    [
        new ComponentDefinition(
            Id: ComponentIds.AnyDesk,
            DisplayName: "AnyDesk",
            Purpose: "Remote help from family (AnyDesk)",
            WingetId: "AnyDesk.AnyDesk",
            RequiresAdmin: true,
            UninstallDisplayNameMatch: "AnyDesk",
            ExeRelativePaths: [@"AnyDesk\AnyDesk.exe"],
            ServiceName: null,
            ProcessName: "AnyDesk",
            StartArguments: []),

        new ComponentDefinition(
            Id: ComponentIds.OpenRgb,
            DisplayName: "OpenRGB",
            Purpose: "RGB lighting control (OpenRGB)",
            WingetId: "OpenRGB.OpenRGB",
            RequiresAdmin: false,
            UninstallDisplayNameMatch: "OpenRGB",
            ExeRelativePaths: [@"OpenRGB\OpenRGB.exe"],
            ServiceName: null,
            ProcessName: "OpenRGB",
            StartArguments: ["--server", "--startminimized"]),

        new ComponentDefinition(
            Id: ComponentIds.PawnIo,
            DisplayName: "PawnIO driver",
            Purpose: "Fan control and temperature sensors (PawnIO driver)",
            WingetId: "namazso.PawnIO",
            RequiresAdmin: true,
            UninstallDisplayNameMatch: "PawnIO",
            ExeRelativePaths: [],
            ServiceName: "PawnIO",
            ProcessName: null,
            StartArguments: null),
    ];

    public static ComponentDefinition Get(string id) =>
        All.FirstOrDefault(c => c.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown component id.");
}
