using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Printing;

namespace Porchlight.App.Features.Printers;

/// <summary>One printer in the list: plain-words status (icon + text), jobs waiting, and what can be done.</summary>
public sealed partial class PrinterRowViewModel : ObservableObject
{
    // Segoe Fluent Icons: CheckMark, Print, Warning, ErrorBadge, Pause.
    private const string OkGlyph = "\uE73E";
    private const string PrintingGlyph = "\uE749";
    private const string WarningGlyph = "\uE7BA";
    private const string ErrorGlyph = "\uEA39";
    private const string PausedGlyph = "\uE769";

    [ObservableProperty]
    private bool _isBusy;

    public PrinterRowViewModel(PrinterEntry entry)
    {
        Entry = entry;
    }

    public PrinterEntry Entry { get; }

    public string Name => Entry.Name;

    public bool IsDefault => Entry.IsDefault;

    public string StatusText => PrinterStatusDecoder.Label(Entry.Status);

    public string StatusGlyph => Entry.Status switch
    {
        PrinterStatusKind.Ready => OkGlyph,
        PrinterStatusKind.Printing => PrintingGlyph,
        PrinterStatusKind.Paused => PausedGlyph,
        PrinterStatusKind.Error => ErrorGlyph,
        _ => WarningGlyph,
    };

    public string JobsText => Entry.JobCount switch
    {
        0 => "Nothing waiting to print",
        1 => "1 document waiting to print",
        _ => $"{Entry.JobCount} documents waiting to print",
    };

    public string ConnectionText => Entry.IsNetwork ? "Connected over the network" : "Connected directly to this PC";

    public bool IsOffline => Entry.WorkOffline || Entry.Status == PrinterStatusKind.Offline;

    public string OfflineHint => Entry.WorkOffline
        ? "Windows has this printer set to \"Use printer offline\", so nothing will print. Use it online to send jobs again."
        : "Windows thinks this printer is switched off or not connected. Check the power and the cable or Wi-Fi.";

    public bool CanUseOnline => Entry.WorkOffline;

    public bool CanMakeDefault => !Entry.IsDefault && !IsBusy;

    public bool CanClearJobs => Entry.JobCount > 0 && !IsBusy;

    public string MakeDefaultAutomationName => $"Make {Entry.Name} the default printer";

    public string ClearJobsAutomationName => $"Clear stuck print jobs for {Entry.Name}";

    public string UseOnlineAutomationName => $"Use {Entry.Name} online";

    public string TestPageAutomationName => $"Print a test page on {Entry.Name}";

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanMakeDefault));
        OnPropertyChanged(nameof(CanClearJobs));
    }
}
