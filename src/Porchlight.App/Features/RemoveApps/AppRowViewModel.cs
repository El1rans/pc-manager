using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Features.Cleanup;
using Porchlight.Core.Monitoring;
using Porchlight.Core.RemoveApps;

namespace Porchlight.App.Features.RemoveApps;

/// <summary>One row of the "Remove apps" page.</summary>
public sealed partial class AppRowViewModel : ObservableObject
{
    public const string PreinstalledText = "Often preinstalled";
    public const string ManagedText = "Managed by Porchlight";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveNow))]
    private bool _isBusy;

    public AppRowViewModel(RemovableApp app, DateOnly today)
    {
        App = app;
        SizeText = app.App.EstimatedSizeBytes is { } bytes ? ByteFormatter.FormatBytes(bytes) : "Size unknown";
        InstalledText = CleanupTextFormatter.FormatInstalled(app.App.InstallDate, today);
    }

    public RemovableApp App { get; }

    public string Name => App.App.DisplayName;

    public string Publisher => App.App.Publisher ?? string.Empty;

    public string SizeText { get; }

    public string InstalledText { get; }

    public long? SizeBytes => App.App.EstimatedSizeBytes;

    public DateOnly? InstallDate => App.App.InstallDate;

    public bool IsManaged => App.Kind == RemovableAppKind.ManagedByPorchlight;

    public bool IsOftenPreinstalled => App.IsOftenPreinstalled;

    public bool CanRemove => App.CanRemove;

    public bool CanRemoveNow => CanRemove && !IsBusy;

    public string RemoveAutomationName => $"Remove {Name}";

    public string RowAutomationName => $"{Name}, {SizeText}";
}
