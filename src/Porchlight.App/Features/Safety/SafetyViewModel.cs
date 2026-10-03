using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Safety;

namespace Porchlight.App.Features.Safety;

/// <summary>"Safety" page: is this PC safe? Three independent cards (security software, Windows
/// Update, remote-control tools). See docs/specs/31-safety-status.md.</summary>
public sealed partial class SafetyViewModel : PageViewModelBase
{
    private const string WindowsSecurityUri = "windowsdefender:";
    private const string WindowsUpdateUri = "ms-settings:windowsupdate";
    private const string CardFailedMessage = "Couldn't check this. Try Refresh.";

    private readonly ISecurityStatusService _security;
    private readonly IWindowsUpdateStatusService _windowsUpdate;
    private readonly IRemoteAccessService _remoteAccess;
    private readonly IUrlLauncher _launcher;
    private readonly ILogger<SafetyViewModel> _logger;

    private SecurityStatus? _securityStatus;
    private WindowsUpdateStatus? _updateStatus;
    private RemoteAccessStatus? _remoteStatus;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _headline = "Checking...";

    [ObservableProperty]
    private string _headlineGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);

    [ObservableProperty]
    private string _securityVerdict = "Checking...";

    [ObservableProperty]
    private string _securityGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);

    [ObservableProperty]
    private string _updateVerdict = "Checking...";

    [ObservableProperty]
    private string _updateGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);

    [ObservableProperty]
    private string _remoteVerdict = "Checking...";

    [ObservableProperty]
    private string _remoteGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);

    [ObservableProperty]
    private string? _pendingMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckPendingCommand))]
    private bool _isCheckingPending;

    public SafetyViewModel(
        ISecurityStatusService security,
        IWindowsUpdateStatusService windowsUpdate,
        IRemoteAccessService remoteAccess,
        IUrlLauncher launcher,
        ILogger<SafetyViewModel> logger)
    {
        _security = security;
        _windowsUpdate = windowsUpdate;
        _remoteAccess = remoteAccess;
        _launcher = launcher;
        _logger = logger;
    }

    public override string Title => "Safety";

    // Segoe Fluent Icons "Shield".
    public override string Glyph => "";

    public override int Order => 0;

    public override PageCategory Category => PageCategory.InternetAndSafety;

    public ObservableCollection<string> SecurityDetails { get; } = [];

    public ObservableCollection<string> UpdateDetails { get; } = [];

    public ObservableCollection<RemoteToolRowViewModel> RemoteTools { get; } = [];

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private void OpenWindowsSecurity() => _launcher.Open(WindowsSecurityUri);

    [RelayCommand]
    private void OpenWindowsUpdate() => _launcher.Open(WindowsUpdateUri);

    [RelayCommand(CanExecute = nameof(CanCheckPending))]
    private async Task CheckPendingAsync()
    {
        IsCheckingPending = true;
        PendingMessage = "Checking for updates. This can take a few minutes...";
        try
        {
            var result = await _windowsUpdate.CheckPendingAsync(CancellationToken.None);
            PendingMessage = result.Message;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Checking for waiting updates failed.");
            PendingMessage = new PendingUpdatesCheck(PendingUpdatesOutcome.Failed, 0).Message;
        }
        finally
        {
            IsCheckingPending = false;
        }
    }

    private bool CanCheckPending() => !IsCheckingPending;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        _securityStatus = null;
        _updateStatus = null;
        _remoteStatus = null;
        try
        {
            await Task.WhenAll(
                RunCardAsync("security", LoadSecurityAsync, cancellationToken),
                RunCardAsync("Windows Update", LoadUpdateAsync, cancellationToken),
                RunCardAsync("remote access", LoadRemoteAsync, cancellationToken));
            UpdateHeadline();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Runs one card's load; a failure shows "Couldn't check" on that card only.</summary>
    private async Task RunCardAsync(string card, Func<CancellationToken, Task> load, CancellationToken cancellationToken)
    {
        try
        {
            await load(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogWarning(ex, "Safety card '{Card}' failed to load.", card);
            switch (card)
            {
                case "security":
                    SecurityVerdict = CardFailedMessage;
                    SecurityGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);
                    break;
                case "Windows Update":
                    UpdateVerdict = CardFailedMessage;
                    UpdateGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);
                    break;
                default:
                    RemoteVerdict = CardFailedMessage;
                    RemoteGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);
                    break;
            }
        }
    }

    private async Task LoadSecurityAsync(CancellationToken cancellationToken)
    {
        var status = await _security.GetAsync(cancellationToken);
        _securityStatus = status;
        SecurityVerdict = status.Verdict;
        SecurityGlyph = SafetyGlyphs.For(status.Level);
        Replace(SecurityDetails, status.Details);
    }

    private async Task LoadUpdateAsync(CancellationToken cancellationToken)
    {
        var status = await _windowsUpdate.GetAsync(cancellationToken);
        _updateStatus = status;
        UpdateVerdict = status.Verdict;
        UpdateGlyph = SafetyGlyphs.For(status.Level);
        Replace(UpdateDetails, status.Details);
    }

    private async Task LoadRemoteAsync(CancellationToken cancellationToken)
    {
        var status = await _remoteAccess.GetAsync(cancellationToken);
        _remoteStatus = status;
        RemoteVerdict = status.Verdict;
        RemoteGlyph = SafetyGlyphs.For(status.Level);
        RemoteTools.Clear();
        foreach (var tool in status.Tools)
        {
            RemoteTools.Add(new RemoteToolRowViewModel(tool));
        }
    }

    private void UpdateHeadline()
    {
        if (_securityStatus is null || _updateStatus is null || _remoteStatus is null)
        {
            Headline = CardFailedMessage;
            HeadlineGlyph = SafetyGlyphs.For(SafetyLevel.Unknown);
            return;
        }

        var summary = new SafetyStatus(_securityStatus, _updateStatus, _remoteStatus);
        Headline = summary.Headline;
        HeadlineGlyph = SafetyGlyphs.For(summary.Level);
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
