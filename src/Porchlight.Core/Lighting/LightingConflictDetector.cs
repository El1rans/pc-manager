using Microsoft.Extensions.Logging;
using Porchlight.Core.Components;

namespace Porchlight.Core.Lighting;

/// <inheritdoc cref="ILightingConflictDetector"/>
public sealed partial class LightingConflictDetector : ILightingConflictDetector
{
    // HKCU\Software\Microsoft\Lighting - Windows' own "Dynamic Lighting" (Settings > Personalization
    // > Dynamic Lighting), which can drive the same devices as OpenRGB through its own HID lamp-array
    // protocol.
    private const string DynamicLightingKey = @"Software\Microsoft\Lighting";
    private const string AmbientLightingEnabledValue = "AmbientLightingEnabled";
    private const string BrightnessValue = "Brightness";

    private const string DynamicLightingWarningId = "windows-dynamic-lighting";
    private const string DynamicLightingSettingsUri = "ms-settings:personalization-lighting";

    private const string DynamicLightingEnabledMessage =
        "Windows Dynamic Lighting is on - it can take over your keyboard/mouse lighting. Turn it off in Settings > Personalization > Dynamic Lighting.";

    private const string DynamicLightingZeroBrightnessMessage =
        "Windows Dynamic Lighting brightness is 0% - devices it controls will turn off. Turn it off (or raise its brightness) in Settings > Personalization > Dynamic Lighting.";

    private readonly IRegistryReader _registryReader;
    private readonly IProcessProbe _processProbe;
    private readonly ILogger<LightingConflictDetector> _logger;

    public LightingConflictDetector(
        IRegistryReader registryReader, IProcessProbe processProbe, ILogger<LightingConflictDetector> logger)
    {
        _registryReader = registryReader;
        _processProbe = processProbe;
        _logger = logger;
    }

    public Task<IReadOnlyList<LightingConflictWarning>> DetectAsync(CancellationToken cancellationToken) =>
        Task.Run(Detect, cancellationToken);

    /// <summary>Registry enumeration and <c>Process.GetProcessesByName</c> are both synchronous
    /// I/O; <see cref="DetectAsync"/> runs this off the caller's thread the same way
    /// <c>ComponentService.Detect</c> does.</summary>
    private IReadOnlyList<LightingConflictWarning> Detect()
    {
        List<LightingConflictWarning> warnings = [];

        try
        {
            var dynamicLighting = DetectDynamicLighting();
            if (dynamicLighting is not null)
            {
                warnings.Add(dynamicLighting);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            LogDynamicLightingCheckFailed(ex);
        }

        foreach (var vendor in VendorLightingSoftware.All)
        {
            try
            {
                if (IsVendorSoftwareActive(vendor))
                {
                    warnings.Add(new LightingConflictWarning(
                        $"vendor-{vendor.Id}",
                        vendor.DisplayName,
                        $"{vendor.DisplayName} is running - it can also control this device's RGB lighting and fight OpenRGB over it. " +
                        $"Close {vendor.DisplayName}, or disable its lighting control, if you want OpenRGB in charge."));
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                LogVendorCheckFailed(vendor.DisplayName, ex);
            }
        }

        return warnings;
    }

    private LightingConflictWarning? DetectDynamicLighting()
    {
        var enabled = _registryReader.GetCurrentUserDwordValue(DynamicLightingKey, AmbientLightingEnabledValue);
        if (enabled != 1)
        {
            return null;
        }

        var brightness = _registryReader.GetCurrentUserDwordValue(DynamicLightingKey, BrightnessValue);
        var message = brightness == 0 ? DynamicLightingZeroBrightnessMessage : DynamicLightingEnabledMessage;

        return new LightingConflictWarning(
            DynamicLightingWarningId, "Windows Dynamic Lighting", message, DynamicLightingSettingsUri, "Open Dynamic Lighting settings");
    }

    private bool IsVendorSoftwareActive(VendorLightingSoftware vendor) =>
        vendor.ProcessNames.Any(_processProbe.IsRunning) || vendor.ServiceNames.Any(_registryReader.ServiceExists);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not check Windows Dynamic Lighting settings for a lighting conflict.")]
    private partial void LogDynamicLightingCheckFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not check whether {Vendor} is running/installed for a lighting conflict.")]
    private partial void LogVendorCheckFailed(string vendor, Exception exception);
}
