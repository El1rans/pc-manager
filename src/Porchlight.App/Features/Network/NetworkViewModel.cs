using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.Elevation;
using Porchlight.Core.Network;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Network;

/// <summary>
/// "Internet" page: connection status, the guided "Fix my internet" check, a speed test the user
/// starts explicitly, and which programs are using the network. See docs/specs/18-network.md.
/// </summary>
public sealed partial class NetworkViewModel : PageViewModelBase, IBusyGuard, IDisposable
{
    private const string NetworkSettingsUri = "ms-settings:network-status";
    private static readonly TimeSpan AppsRefreshInterval = TimeSpan.FromSeconds(3);

    private readonly INetworkStatusProvider _statusProvider;
    private readonly INetworkTroubleshooter _troubleshooter;
    private readonly INetworkRemedyService _remedies;
    private readonly ISpeedTestService _speedTest;
    private readonly INetworkAppUsageService _appUsage;
    private readonly IElevationService _elevation;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ISettingsStore _settings;
    private readonly ILogger<NetworkViewModel> _logger;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private CancellationTokenSource? _appsCts;
    private CancellationTokenSource? _speedCts;
    private bool _disposed;

    // Status card
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionTypeText = "Checking...";

    [ObservableProperty]
    private bool _showWifi;

    [ObservableProperty]
    private string _wifiName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBar1), nameof(HasBar2), nameof(HasBar3), nameof(HasBar4))]
    private int _signalBars;

    [ObservableProperty]
    private string _signalLabel = string.Empty;

    [ObservableProperty]
    private string _localIp = "-";

    [ObservableProperty]
    private string _gateway = "-";

    [ObservableProperty]
    private string _dnsServers = "-";

    // Troubleshooter
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunTroubleshooterCommand))]
    private bool _isTroubleshooting;

    [ObservableProperty]
    private bool _hasDiagnosis;

    [ObservableProperty]
    private bool _diagnosisIsHealthy;

    [ObservableProperty]
    private string _diagnosisHeadline = string.Empty;

    [ObservableProperty]
    private string _diagnosisAdvice = string.Empty;

    [ObservableProperty]
    private bool _showAdminBanner;

    // Remedies
    [ObservableProperty]
    private RemedyViewModel? _pendingRemedy;

    [ObservableProperty]
    private string _pendingRemedyWarning = string.Empty;

    [ObservableProperty]
    private bool _isRemedyRunning;

    [ObservableProperty]
    private string _remedyMessage = string.Empty;

    // Speed test
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartSpeedTestCommand))]
    private bool _isSpeedTesting;

    [ObservableProperty]
    private string _speedProgressText = string.Empty;

    [ObservableProperty]
    private bool _hasSpeedResult;

    [ObservableProperty]
    private string _downloadText = "-";

    [ObservableProperty]
    private string _uploadText = "-";

    [ObservableProperty]
    private string _latencyText = "-";

    [ObservableProperty]
    private string _verdictHeadline = string.Empty;

    [ObservableProperty]
    private string _verdictDetail = string.Empty;

    [ObservableProperty]
    private string _verdictGlyph = "";

    [ObservableProperty]
    private string _lastTestedText = string.Empty;

    [ObservableProperty]
    private string _speedMessage = string.Empty;

    // Apps
    [ObservableProperty]
    private bool _hasApps;

    public NetworkViewModel(
        INetworkStatusProvider statusProvider,
        INetworkTroubleshooter troubleshooter,
        INetworkRemedyService remedies,
        ISpeedTestService speedTest,
        INetworkAppUsageService appUsage,
        IElevationService elevation,
        IUrlLauncher urlLauncher,
        ISettingsStore settings,
        ILogger<NetworkViewModel> logger)
    {
        _statusProvider = statusProvider;
        _troubleshooter = troubleshooter;
        _remedies = remedies;
        _speedTest = speedTest;
        _appUsage = appUsage;
        _elevation = elevation;
        _urlLauncher = urlLauncher;
        _settings = settings;
        _logger = logger;

        Steps = new ObservableCollection<TroubleshootStepViewModel>(
            Enum.GetValues<TroubleshootStep>().Select(s => new TroubleshootStepViewModel(s)));

        if (settings.Current.Network.LastSpeedTest is { } last)
        {
            ShowSpeedResult(last);
        }
    }

    public override string Title => "Internet";

    public override string Glyph => "";

    public override int Order => 7;

    public ObservableCollection<TroubleshootStepViewModel> Steps { get; }

    public ObservableCollection<RemedyViewModel> SuggestedRemedies { get; } = [];

    public ObservableCollection<NetworkAppRow> Apps { get; } = [];

    public bool HasBar1 => SignalBars >= 1;

    public bool HasBar2 => SignalBars >= 2;

    public bool HasBar3 => SignalBars >= 3;

    public bool HasBar4 => SignalBars >= 4;

    public bool HasPendingRemedy => PendingRemedy is not null;

    public bool IsBusyWithWork => IsTroubleshooting || IsRemedyRunning || IsSpeedTesting;

    public string BusyMessage =>
        "Porchlight is still checking or fixing your internet. Closing now stops it partway. Close anyway?";

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        await RefreshStatusAsync().ConfigureAwait(true);
    }

    /// <summary>Called by the view when it becomes visible or hidden; the app list only refreshes
    /// while visible.</summary>
    public void SetPageVisible(bool visible)
    {
        if (_disposed)
        {
            return;
        }

        if (visible && _appsCts is null)
        {
            _appsCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _ = RunAppsLoopAsync(_appsCts.Token);
        }
        else if (!visible && _appsCts is not null)
        {
            _appsCts.Cancel();
            _appsCts.Dispose();
            _appsCts = null;
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        try
        {
            var status = await Task.Run(_statusProvider.GetStatus, _lifetimeCts.Token).ConfigureAwait(true);
            ApplyStatus(status);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Status refresh cancelled.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunTroubleshooter))]
    private async Task RunTroubleshooterAsync()
    {
        IsTroubleshooting = true;
        HasDiagnosis = false;
        RemedyMessage = string.Empty;
        PendingRemedy = null;
        foreach (var step in Steps)
        {
            step.State = StepState.Pending;
        }

        try
        {
            // Progress is created on the UI thread, so updates arrive there.
            var progress = new Progress<TroubleshootStepUpdate>(update =>
                Steps.First(s => s.Step == update.Step).State = update.State);
            var report = await _troubleshooter.RunAsync(progress, _lifetimeCts.Token).ConfigureAwait(true);
            ShowDiagnosis(report.Diagnosis);
            await RefreshStatusAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Troubleshooter cancelled.");
        }
        finally
        {
            IsTroubleshooting = false;
        }
    }

    private bool CanRunTroubleshooter() => !IsTroubleshooting && !IsRemedyRunning;

    [RelayCommand]
    private async Task RequestRemedyAsync(RemedyViewModel? remedy)
    {
        if (remedy is null || IsRemedyRunning)
        {
            return;
        }

        RemedyMessage = string.Empty;
        switch (remedy.Kind)
        {
            case RemedyKind.OpenNetworkSettings:
                _urlLauncher.Open(NetworkSettingsUri);
                break;
            case RemedyKind.FlushDns:
                await ExecuteRemedyAsync(remedy).ConfigureAwait(true);
                break;
            case RemedyKind.RenewIp:
                PendingRemedy = remedy;
                PendingRemedyWarning = "Your internet will drop for a few seconds. Continue?";
                break;
            case RemedyKind.ResetAdapter:
                if (!_elevation.IsElevated)
                {
                    ShowAdminBanner = true;
                    RemedyMessage = "This needs administrator rights. Use the button above to restart Porchlight as administrator, then try again.";
                    break;
                }

                PendingRemedy = remedy;
                PendingRemedyWarning = "Your network connection will be switched off and on. Your internet drops for several seconds. Continue?";
                break;
        }
    }

    [RelayCommand]
    private async Task ConfirmRemedyAsync()
    {
        if (PendingRemedy is { } remedy)
        {
            PendingRemedy = null;
            await ExecuteRemedyAsync(remedy).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void CancelRemedy() => PendingRemedy = null;

    [RelayCommand(CanExecute = nameof(CanStartSpeedTest))]
    private async Task StartSpeedTestAsync()
    {
        IsSpeedTesting = true;
        SpeedMessage = string.Empty;
        _speedCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        try
        {
            var progress = new Progress<SpeedTestPhase>(phase => SpeedProgressText = phase switch
            {
                SpeedTestPhase.Latency => "Checking the delay...",
                SpeedTestPhase.Download => "Testing download speed...",
                _ => "Testing upload speed...",
            });
            var result = await _speedTest.RunAsync(progress, _speedCts.Token).ConfigureAwait(true);
            if (result is null)
            {
                SpeedMessage = "The test couldn't reach the speed test service. Check your connection and try again.";
            }
            else
            {
                _settings.Update(s => s.Network.LastSpeedTest = result);
                ShowSpeedResult(result);
            }
        }
        catch (OperationCanceledException)
        {
            SpeedMessage = "Speed test stopped.";
        }
        finally
        {
            _speedCts.Dispose();
            _speedCts = null;
            SpeedProgressText = string.Empty;
            IsSpeedTesting = false;
        }
    }

    private bool CanStartSpeedTest() => !IsSpeedTesting;

    [RelayCommand]
    private void CancelSpeedTest() => _speedCts?.Cancel();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCts.Cancel();
        _appsCts?.Dispose();
        _speedCts?.Dispose();
        _lifetimeCts.Dispose();
    }

    partial void OnPendingRemedyChanged(RemedyViewModel? value) => OnPropertyChanged(nameof(HasPendingRemedy));

    partial void OnIsTroubleshootingChanged(bool value) => OnPropertyChanged(nameof(IsBusyWithWork));

    partial void OnIsRemedyRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusyWithWork));
        RunTroubleshooterCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSpeedTestingChanged(bool value) => OnPropertyChanged(nameof(IsBusyWithWork));

    private async Task ExecuteRemedyAsync(RemedyViewModel remedy)
    {
        IsRemedyRunning = true;
        try
        {
            var token = _lifetimeCts.Token;
            var result = remedy.Kind switch
            {
                RemedyKind.FlushDns => await _remedies.FlushDnsAsync(token).ConfigureAwait(true),
                RemedyKind.RenewIp => await _remedies.RenewIpAsync(token).ConfigureAwait(true),
                _ => await _remedies.ResetAdapterAsync(
                    (await Task.Run(_statusProvider.GetStatus, token).ConfigureAwait(true)).AdapterId ?? string.Empty,
                    token).ConfigureAwait(true),
            };

            RemedyMessage = result.Message;
            ShowAdminBanner = result.Outcome == RemedyOutcome.NeedsAdmin;
        }
        catch (OperationCanceledException)
        {
            RemedyMessage = "Stopped.";
        }
        finally
        {
            IsRemedyRunning = false;
        }
    }

    private void ShowDiagnosis(Diagnosis diagnosis)
    {
        DiagnosisHeadline = diagnosis.Headline;
        DiagnosisAdvice = diagnosis.Advice;
        DiagnosisIsHealthy = diagnosis.Outcome == DiagnosisOutcome.Healthy;
        SuggestedRemedies.Clear();
        foreach (var kind in diagnosis.Remedies)
        {
            SuggestedRemedies.Add(new RemedyViewModel(kind));
        }

        ShowAdminBanner = !_elevation.IsElevated && diagnosis.Remedies.Contains(RemedyKind.ResetAdapter);
        HasDiagnosis = true;
    }

    private void ApplyStatus(NetworkStatus status)
    {
        IsConnected = status.IsConnected;
        ConnectionTypeText = status.ConnectionType switch
        {
            ConnectionType.WiFi => "Wi-Fi",
            ConnectionType.Ethernet => "Network cable",
            ConnectionType.Other => "Other",
            _ => "None",
        };

        ShowWifi = status.ConnectionType == ConnectionType.WiFi;
        if (status.Wifi is { } wifi)
        {
            WifiName = wifi.Ssid ?? "Name hidden by Windows";
            var signal = WifiSignalDescriber.Describe(wifi.SignalPercent);
            SignalBars = signal.Bars;
            SignalLabel = signal.Label;
        }
        else
        {
            WifiName = "Not available";
            SignalBars = 0;
            SignalLabel = string.Empty;
        }

        LocalIp = status.LocalIp ?? "-";
        Gateway = status.Gateway ?? "-";
        DnsServers = status.DnsServers.Count > 0 ? string.Join(", ", status.DnsServers) : "-";
    }

    private void ShowSpeedResult(SpeedTestResult result)
    {
        HasSpeedResult = true;
        DownloadText = FormatMbps(result.DownloadMbps);
        UploadText = FormatMbps(result.UploadMbps);
        LatencyText = result.LatencyMs is { } ms ? string.Create(CultureInfo.CurrentCulture, $"{ms:0} ms") : "-";
        var verdict = SpeedVerdictDescriber.Describe(result);
        VerdictHeadline = verdict.Headline;
        VerdictDetail = verdict.Detail;
        VerdictGlyph = verdict.Level is SpeedVerdictLevel.Poor or SpeedVerdictLevel.Unknown ? "" : "";
        LastTestedText = string.Create(CultureInfo.CurrentCulture, $"Tested {result.MeasuredAt.ToLocalTime():f}");
    }

    private static string FormatMbps(double? mbps) =>
        mbps is { } v ? string.Create(CultureInfo.CurrentCulture, $"{v:0.#} Mbps") : "-";

    private async Task RunAppsLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(AppsRefreshInterval);
            do
            {
                await RefreshAppsAsync(cancellationToken).ConfigureAwait(true);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            // Leaving the page (or shutting down) stops the refresh; that is the intent.
            _logger.LogDebug("App list refresh stopped.");
        }
    }

    private async Task RefreshAppsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var usage = await Task.Run(_appUsage.GetUsage, cancellationToken).ConfigureAwait(true);
            Apps.Clear();
            foreach (var app in usage)
            {
                Apps.Add(new NetworkAppRow(app));
            }

            HasApps = Apps.Count > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not refresh the list of apps using the internet.");
        }
    }
}
