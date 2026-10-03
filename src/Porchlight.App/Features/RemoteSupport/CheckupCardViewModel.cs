using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Shell;
using Porchlight.Core.Checkup;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.RemoteSupport;

/// <summary>
/// The "Send a check-up to your helper" card on the Get help page: builds the report, shows exactly
/// what would be shared, and lets the user copy it, save it, or open it in their mail program.
/// Porchlight never sends or uploads anything itself - see docs/specs/16-checkup-report.md.
/// </summary>
public sealed partial class CheckupCardViewModel : ObservableObject
{
    private const string SaveFilter = "Web page (*.html)|*.html|Text file (*.txt)|*.txt";
    private const string TextExtension = ".txt";

    private readonly ICheckupReportBuilder _builder;
    private readonly IClipboardService _clipboard;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IFileDialogService _fileDialogs;
    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;
    private readonly CheckupReminderScheduler _reminderScheduler;
    private readonly ILogger<CheckupCardViewModel> _logger;

    private CheckupReport? _report;
    private bool _loadingEmail;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _helperName = string.Empty;

    [ObservableProperty]
    private string _helperEmail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCheckupCommand))]
    private bool _isBuilding;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    [NotifyCanExecuteChangedFor(nameof(CopyReportCommand), nameof(SaveReportCommand), nameof(EmailReportCommand))]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _statusIsError;

    public CheckupCardViewModel(
        ICheckupReportBuilder builder,
        IClipboardService clipboard,
        IUrlLauncher urlLauncher,
        IFileDialogService fileDialogs,
        ISettingsStore settingsStore,
        TimeProvider timeProvider,
        CheckupReminderScheduler reminderScheduler,
        ILogger<CheckupCardViewModel> logger)
    {
        _reminderScheduler = reminderScheduler;
        _builder = builder;
        _clipboard = clipboard;
        _urlLauncher = urlLauncher;
        _fileDialogs = fileDialogs;
        _settingsStore = settingsStore;
        _timeProvider = timeProvider;
        _logger = logger;

        _loadingEmail = true;
        _helperEmail = settingsStore.Current.RemoteSupport.HelperEmail;
        _loadingEmail = false;
        RefreshReminderInfo();
    }

    /// <summary>"Last check-up: ..." line; empty until the first report.</summary>
    [ObservableProperty]
    private string _lastCheckupText = string.Empty;

    /// <summary>"Next reminder: ..." line; empty while reminders are off.</summary>
    [ObservableProperty]
    private string _nextReminderText = string.Empty;

    /// <summary>Re-reads the reminder settings; called when the page is shown and after each report action.</summary>
    public void RefreshReminderInfo()
    {
        var reminder = _settingsStore.Current.CheckupReminder;
        var last = _reminderScheduler.LastReportDate(reminder);
        LastCheckupText = last is null ? string.Empty : "Last check-up: " + FormatDate(last.Value);
        var next = _reminderScheduler.NextReminderDate(reminder);
        NextReminderText = next is null ? string.Empty : "Next reminder: " + FormatDate(next.Value < DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)
            ? DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime)
            : next.Value);
    }

    private static string FormatDate(DateOnly date) => date.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Remembers that a check-up was made, so the reminder waits a full period.</summary>
    private void RecordReportActivity()
    {
        var now = _timeProvider.GetUtcNow();
        _settingsStore.Update(s => s.CheckupReminder.LastReportCreatedUtc = now);
        RefreshReminderInfo();
    }

    public string Title => $"Send a check-up to {(string.IsNullOrWhiteSpace(HelperName) ? "your helper" : HelperName)}";

    public bool HasReport => PreviewText.Length > 0;

    [RelayCommand(CanExecute = nameof(CanCreateCheckup))]
    private async Task CreateCheckupAsync()
    {
        IsBuilding = true;
        StatusMessage = string.Empty;
        try
        {
            // Off the UI thread: some sections read WMI or run a short AnyDesk command.
            var report = await Task.Run(() => _builder.BuildAsync(CancellationToken.None)).ConfigureAwait(true);
            _report = report;
            PreviewText = CheckupTextRenderer.Render(report);
            RecordReportActivity();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not build the check-up report.");
            SetStatus("Something went wrong making the check-up. Please try again.", isError: true);
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private bool CanCreateCheckup() => !IsBuilding;

    [RelayCommand(CanExecute = nameof(HasReport))]
    private void CopyReport()
    {
        var copied = _clipboard.SetText(PreviewText);
        if (copied)
        {
            RecordReportActivity();
        }

        SetStatus(copied ? "Copied. You can paste it into a message." : "Couldn't copy. Please try again.", isError: !copied);
    }

    [RelayCommand(CanExecute = nameof(HasReport))]
    private async Task SaveReportAsync()
    {
        if (_report is null)
        {
            return;
        }

        var defaultName = "Porchlight check-up " + _timeProvider.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".html";
        var path = _fileDialogs.PickSaveFile(
            "Save check-up report", defaultName, SaveFilter, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        if (path is null)
        {
            return;
        }

        var content = string.Equals(Path.GetExtension(path), TextExtension, StringComparison.OrdinalIgnoreCase)
            ? PreviewText
            : CheckupHtmlRenderer.Render(_report);

        try
        {
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)).ConfigureAwait(true);
            RecordReportActivity();
            SetStatus($"Saved to {Path.GetFileName(path)}.", isError: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save the check-up report.");
            SetStatus("Couldn't save the file. Try another folder.", isError: true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasReport))]
    private void EmailReport()
    {
        var subject = $"Porchlight check-up for {_report?.ComputerName ?? Environment.MachineName}";
        _urlLauncher.Open(CheckupMailto.Build(HelperEmail, subject, PreviewText));
        RecordReportActivity();
        SetStatus("Your email program should open with the report in a new message. Nothing is sent until you press Send.", isError: false);
    }

    /// <summary>Called by the Get help page when the helper name changes.</summary>
    public void SetHelperName(string name) => HelperName = name;

    partial void OnHelperEmailChanged(string value)
    {
        if (_loadingEmail)
        {
            return;
        }

        _settingsStore.Update(s => s.RemoteSupport.HelperEmail = value.Trim());
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
