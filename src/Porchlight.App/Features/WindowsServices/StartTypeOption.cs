using Porchlight.Core.WindowsServices;

namespace Porchlight.App.Features.WindowsServices;

/// <summary>One of the four start choices offered in the combo box.</summary>
public sealed record StartTypeOption(ServiceStartType Type, string Label)
{
    public static IReadOnlyList<StartTypeOption> All { get; } =
    [
        new(ServiceStartType.Automatic, ServiceClassifier.StartTypeLabel(ServiceStartType.Automatic)),
        new(ServiceStartType.AutomaticDelayed, ServiceClassifier.StartTypeLabel(ServiceStartType.AutomaticDelayed)),
        new(ServiceStartType.Manual, ServiceClassifier.StartTypeLabel(ServiceStartType.Manual)),
        new(ServiceStartType.Disabled, ServiceClassifier.StartTypeLabel(ServiceStartType.Disabled)),
    ];

    /// <summary>The option for <paramref name="type"/>.</summary>
    public static StartTypeOption For(ServiceStartType type) => All.First(o => o.Type == type);
}
