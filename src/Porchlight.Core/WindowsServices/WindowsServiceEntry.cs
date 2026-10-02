namespace Porchlight.Core.WindowsServices;

/// <summary>A listed service, ready to show.</summary>
/// <param name="Name">Service (key) name; the id used for changes.</param>
/// <param name="DisplayName">Name shown in services.msc.</param>
/// <param name="Description">First sentence of the service's description, or empty.</param>
/// <param name="Publisher">Company of the service executable, or null.</param>
/// <param name="StartType">Current start type.</param>
/// <param name="State">Current run state.</param>
/// <param name="IsMicrosoft">Windows/Microsoft service: shown read-only, never changed.</param>
/// <param name="IsChangeable">Whether Porchlight may change it (not Microsoft, not managed elsewhere).</param>
public sealed record WindowsServiceEntry(
    string Name,
    string DisplayName,
    string Description,
    string? Publisher,
    ServiceStartType StartType,
    ServiceRunState State,
    bool IsMicrosoft,
    bool IsChangeable);
