namespace PCManager.Core.Components;

/// <summary>
/// Static description of one third-party tool a feature depends on: how to show it, how to
/// install it, and how to detect whether it is already there. See <see cref="ComponentCatalog"/>
/// for the three components milestone 01b defines (AnyDesk, OpenRGB, the PawnIO driver).
/// </summary>
/// <param name="Id">Stable identifier used everywhere else (settings, <see cref="IComponentService"/>
/// calls) - see <see cref="ComponentIds"/>.</param>
/// <param name="DisplayName">Name shown on the <c>ComponentCard</c> and in first-run setup.</param>
/// <param name="Purpose">One short, plain-language sentence explaining why the app needs this,
/// e.g. "Remote help from family (AnyDesk)".</param>
/// <param name="WingetId">Exact winget package identifier, passed with <c>--exact</c>.</param>
/// <param name="RequiresAdmin">Whether installing this typically triggers a UAC prompt.</param>
/// <param name="UninstallDisplayNameMatch">Substring to look for in the uninstall registry's
/// <c>DisplayName</c> values.</param>
/// <param name="ExeRelativePaths">Relative paths (under a Program Files directory, or under the
/// uninstall entry's <c>InstallLocation</c>) where the executable is expected.</param>
/// <param name="ServiceName">For a kernel-driver component, the Windows service name that must
/// also be present for the component to count as installed; null when not applicable.</param>
/// <param name="ProcessName">Name (without ".exe") of the process that represents this component
/// running, used for <see cref="ComponentState.Running"/> detection; null when the component has
/// no separate "running" state to track.</param>
/// <param name="StartArguments">Arguments to launch the component with when it needs to be
/// started (e.g. OpenRGB's <c>--server --startminimized</c>); null when
/// <see cref="IComponentService.StartAsync"/> does not apply to this component.</param>
public sealed record ComponentDefinition(
    string Id,
    string DisplayName,
    string Purpose,
    string WingetId,
    bool RequiresAdmin,
    string UninstallDisplayNameMatch,
    IReadOnlyList<string> ExeRelativePaths,
    string? ServiceName,
    string? ProcessName,
    IReadOnlyList<string>? StartArguments);
