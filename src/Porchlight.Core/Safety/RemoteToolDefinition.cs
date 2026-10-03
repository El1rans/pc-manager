namespace Porchlight.Core.Safety;

/// <summary>A known remote-control tool and the traces it leaves on a PC.</summary>
/// <param name="Id">Stable id; AnyDesk uses <c>ComponentIds.AnyDesk</c> so Porchlight can recognise its own.</param>
/// <param name="Name">Name shown to the user.</param>
/// <param name="ProcessNames">Exact process names (no ".exe"), compared ignoring case.</param>
/// <param name="AppNameFragments">Fragments of the uninstall-list <c>DisplayName</c>, compared ignoring case.</param>
/// <param name="ServiceNameFragments">Fragments of a Windows service name or display name, compared ignoring case.</param>
public sealed record RemoteToolDefinition(
    string Id,
    string Name,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> AppNameFragments,
    IReadOnlyList<string> ServiceNameFragments);
