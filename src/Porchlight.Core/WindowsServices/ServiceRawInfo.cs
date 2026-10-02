namespace Porchlight.Core.WindowsServices;

/// <summary>One <c>Win32_Service</c> row as read, before classification.</summary>
/// <param name="Name">Service (key) name.</param>
/// <param name="DisplayName">Name shown in services.msc.</param>
/// <param name="Description">The service's own description, or null.</param>
/// <param name="PathName">Raw image path including arguments, or null.</param>
/// <param name="StartMode">WMI start mode: Boot, System, Auto, Manual, Disabled.</param>
/// <param name="DelayedAutoStart">True when auto-start is delayed.</param>
/// <param name="State">WMI state: Running, Stopped, Start Pending, Stop Pending, ...</param>
/// <param name="ServiceType">WMI type: Own Process, Share Process, Kernel Driver, ...</param>
public sealed record ServiceRawInfo(
    string Name,
    string DisplayName,
    string? Description,
    string? PathName,
    string? StartMode,
    bool DelayedAutoStart,
    string? State,
    string? ServiceType);
