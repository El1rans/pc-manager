using Porchlight.Core.Cleanup;
using Porchlight.Core.Components;

namespace Porchlight.Core.RemoveApps;

/// <summary>Decides which installed programs are Porchlight itself, its managed components, or
/// runtimes and drivers the PC usually needs. Pure data and matching, so it is unit-testable.</summary>
public static class AppProtectionRules
{
    private const string PorchlightName = "Porchlight";
    private const string MicrosoftPublisher = "Microsoft";
    private const string WindowsNamePrefix = "Windows ";

    private static readonly string[] SystemPartNameFragments =
    [
        "Visual C++",
        "Microsoft .NET",
        ".NET Runtime",
        ".NET Desktop Runtime",
        ".NET Host",
        "ASP.NET Core",
        "Windows Desktop Runtime",
        "Windows App SDK",
        "Windows Application Runtime",
        "WebView2",
        "Microsoft Edge",
        "Microsoft Update Health Tools",
        "Driver",
        "Chipset",
        "Management Engine",
        "PhysX",
        "AMD Software",
    ];

    private static readonly string[] SystemPartPublisherFragments = ["Realtek"];

    public static RemovableAppKind Classify(InstalledApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var name = app.DisplayName;
        if (name.Contains(PorchlightName, StringComparison.OrdinalIgnoreCase))
        {
            return RemovableAppKind.Porchlight;
        }

        if (ComponentCatalog.All.Any(c => name.Contains(c.UninstallDisplayNameMatch, StringComparison.OrdinalIgnoreCase)))
        {
            return RemovableAppKind.ManagedByPorchlight;
        }

        return IsSystemPart(app) ? RemovableAppKind.SystemPart : RemovableAppKind.Normal;
    }

    private static bool IsSystemPart(InstalledApp app)
    {
        if (SystemPartNameFragments.Any(f => app.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var publisher = app.Publisher ?? string.Empty;
        if (SystemPartPublisherFragments.Any(f => publisher.Contains(f, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return publisher.Contains(MicrosoftPublisher, StringComparison.OrdinalIgnoreCase)
            && app.DisplayName.StartsWith(WindowsNamePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
