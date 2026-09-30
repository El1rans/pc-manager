using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.SelfUpdate;

namespace Porchlight.App.Features.Updates;

/// <summary>
/// The "Porchlight X.Y.Z is available" card at the top of the Updates page: checks GitHub Releases
/// for a newer Porchlight, and for an installed copy downloads, verifies and runs the installer, then
/// exits so Setup can replace the exe. A portable copy only gets a link to the release page. See
/// <c>docs/specs/23-self-update.md</c>. Every failure is shown as a plain-language line on the card
/// (<see cref="ErrorText"/>); a failed <em>check</em> is silent (only logged), since being offline
/// is not something to nag about.
/// </summary>
public sealed partial class PorchlightUpdateViewModel : ObservableObject, IDisposable
{
    private readonly IReleaseChecker _releaseChecker;
    private readonly IUpdateDownloader _downloader;
    private readonly IInstallerLauncher _installerLauncher;
    private readonly IInstallTypeDetector _installTypeDetector;
    private readonly IRunningAppInfo _appInfo;
    private readonly IUrlLauncher _urlLauncher;
    private readonly IAppLifetime _appLifetime;
    private readonly ILogger<PorchlightUpdateViewModel> _logger;
    private readonly Lock _checkLock = new();

    private Task? _runningCheck;
    private CancellationTokenSource? _updateCts;
    private ReleaseInfo? _release;
    private InstallType _installType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline))]
    [NotifyPropertyChangedFor(nameof(CanUpdateNow))]
    [NotifyPropertyChangedFor(nameof(ShowManualDownload))]
    [NotifyPropertyChangedFor(nameof(ManualDownloadText))]
    private bool _isUpdateAvailable;

    /// <summary>True from pressing "Update now" until the installer has started (or it failed/was cancelled).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpdateNowButton))]
    [NotifyCanExecuteChangedFor(nameof(UpdateNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelUpdateCommand))]
    private bool _isWorking;

    /// <summary>"Downloading... 42%", "Verifying...", "Starting installer...".</summary>
    [ObservableProperty]
    private string? _statusText;

    /// <summary>Download progress, 0-100. Meaningful while <see cref="IsProgressIndeterminate"/> is false.</summary>
    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _isProgressIndeterminate = true;

    /// <summary>The last update attempt's failure, in plain language, or null.</summary>
    [ObservableProperty]
    private string? _errorText;

    public PorchlightUpdateViewModel(
        IReleaseChecker releaseChecker, IUpdateDownloader downloader, IInstallerLauncher installerLauncher,
        IInstallTypeDetector installTypeDetector, IRunningAppInfo appInfo, IUrlLauncher urlLauncher,
        IAppLifetime appLifetime, ILogger<PorchlightUpdateViewModel> logger)
    {
        _releaseChecker = releaseChecker;
        _downloader = downloader;
        _installerLauncher = installerLauncher;
        _installTypeDetector = installTypeDetector;
        _appInfo = appInfo;
        _urlLauncher = urlLauncher;
        _appLifetime = appLifetime;
        _logger = logger;
    }

    /// <summary>The newer release, or null when up to date / not yet checked / the check failed.</summary>
    public ReleaseInfo? Release => _release;

    /// <summary>"Porchlight 0.2.0 is available - you have 0.1.0".</summary>
    public string Headline => _release is null
        ? string.Empty
        : string.Create(
            CultureInfo.InvariantCulture,
            $"Porchlight {_release.Version.ToString(3)} is available - you have {_appInfo.Version.ToString(3)}");

    /// <summary>An installed copy with a downloadable installer can update itself.</summary>
    public bool CanUpdateNow => IsUpdateAvailable && _installType == InstallType.Installed && _release is { CanInstallAutomatically: true };

    public bool ShowUpdateNowButton => CanUpdateNow && !IsWorking;

    /// <summary>Show the "Download" link (portable copy, or a release without an installer attached).</summary>
    public bool ShowManualDownload => IsUpdateAvailable && !CanUpdateNow;

    public string ManualDownloadText => _installType == InstallType.Portable
        ? "Automatic updates need the installed version of Porchlight. Download the new version from the release page."
        : "This release can't be installed automatically. Download it from the release page.";

    /// <summary>
    /// Asks GitHub for the newest release and updates the card. Safe to call from anywhere: a check
    /// already in flight is shared, and a failed or offline check leaves the card as it was.
    /// Continuations resume on the caller's context, so call it on the UI thread.
    /// </summary>
    public Task CheckAsync()
    {
        lock (_checkLock)
        {
            if (_runningCheck is { IsCompleted: false } running)
            {
                return running;
            }

            return _runningCheck = RunCheckAsync();
        }
    }

    private async Task RunCheckAsync()
    {
        try
        {
            // Not while an update is downloading: the card must not change under the running operation.
            if (IsWorking)
            {
                return;
            }

            var result = await _releaseChecker.CheckAsync(CancellationToken.None).ConfigureAwait(true);
            switch (result.Status)
            {
                case SelfUpdateCheckStatus.UpdateAvailable when result.Release is not null:
                    _release = result.Release;
                    _installType = _installTypeDetector.Detect();
                    ErrorText = null;
                    OnPropertyChanged(nameof(Release));
                    IsUpdateAvailable = true;
                    OnPropertyChanged(nameof(Headline));
                    OnPropertyChanged(nameof(CanUpdateNow));
                    OnPropertyChanged(nameof(ShowUpdateNowButton));
                    OnPropertyChanged(nameof(ShowManualDownload));
                    OnPropertyChanged(nameof(ManualDownloadText));
                    break;
                case SelfUpdateCheckStatus.UpToDate:
                    _release = null;
                    OnPropertyChanged(nameof(Release));
                    IsUpdateAvailable = false;
                    ErrorText = null;
                    break;
                default:
                    // Couldn't check: keep whatever was known before.
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The checker never throws for network problems; this is a last-resort net so a bug
            // here can't take down the startup check.
            LogCheckFailed(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartUpdate))]
    private async Task UpdateNowAsync()
    {
        var release = _release;
        if (release is null || !CanUpdateNow)
        {
            return;
        }

        ErrorText = null;
        IsWorking = true;
        IsProgressIndeterminate = true;
        ProgressPercent = 0;
        StatusText = "Downloading...";
        var cts = new CancellationTokenSource();
        _updateCts = cts;
        var started = false;

        try
        {
            var progress = new Progress<UpdateDownloadProgress>(OnDownloadProgress);
            var installerPath = await _downloader.DownloadAsync(release, progress, cts.Token).ConfigureAwait(true);

            IsProgressIndeterminate = true;
            StatusText = "Starting installer...";
            switch (_installerLauncher.Launch(installerPath))
            {
                case InstallerLaunchResult.Started:
                    started = true;
                    // The real exit path (same as the tray's Quit), so fans are handed back to the BIOS
                    // and the app mutexes are released for Setup to proceed.
                    _appLifetime.Shutdown();
                    break;
                case InstallerLaunchResult.Declined:
                    ErrorText = "The update wasn't installed because the Windows permission prompt was declined. Press Update now to try again.";
                    break;
                default:
                    ErrorText = "Porchlight couldn't start the installer. Please try again, or download the update from the release page.";
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the user (or the app is closing); nothing to report.
        }
        catch (SelfUpdateException ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_updateCts, cts))
            {
                _updateCts = null;
            }

            cts.Dispose();
            if (!started)
            {
                IsWorking = false;
                StatusText = null;
            }
        }
    }

    private bool CanStartUpdate() => !IsWorking;

    [RelayCommand(CanExecute = nameof(IsWorking))]
    private void CancelUpdate() => _updateCts?.Cancel();

    /// <summary>"What's new" link - opens the release page on github.com.</summary>
    [RelayCommand]
    private void OpenReleaseNotes()
    {
        if (_release is not null)
        {
            _urlLauncher.Open(_release.ReleasePageUrl.AbsoluteUri);
        }
    }

    private void OnDownloadProgress(UpdateDownloadProgress progress)
    {
        if (progress.Phase == UpdateDownloadPhase.Verifying)
        {
            IsProgressIndeterminate = true;
            StatusText = "Verifying...";
        }
        else if (progress.Fraction is { } fraction)
        {
            IsProgressIndeterminate = false;
            ProgressPercent = Math.Round(fraction * 100);
            StatusText = string.Create(CultureInfo.InvariantCulture, $"Downloading... {ProgressPercent:0}%");
        }
        else
        {
            IsProgressIndeterminate = true;
            StatusText = "Downloading...";
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected failure while checking for a new Porchlight version.")]
    private partial void LogCheckFailed(Exception exception);

    public void Dispose() => _updateCts?.Cancel();
}
