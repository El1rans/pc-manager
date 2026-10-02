using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.Network;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.App.Tests.TestDoubles;
using Porchlight.Core.Network;
using Xunit;

namespace Porchlight.App.Tests.Features.Network;

public sealed class NetworkViewModelTests
{
    private static readonly DateTimeOffset Measured = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeStatusProvider _status = new();
    private readonly FakeTroubleshooter _troubleshooter = new();
    private readonly FakeRemedyService _remedies = new();
    private readonly FakeSpeedTestService _speedTest = new();
    private readonly FakeAppUsageService _appUsage = new();
    private readonly FakeElevationService _elevation = new();
    private readonly FakeUrlLauncher _urlLauncher = new();
    private readonly FakeSettingsStore _settings = new();

    private NetworkViewModel Create() => new(
        _status,
        _troubleshooter,
        _remedies,
        _speedTest,
        _appUsage,
        _elevation,
        _urlLauncher,
        _settings,
        NullLogger<NetworkViewModel>.Instance);

    private static NetworkStatus Status(
        ConnectionType type = ConnectionType.Ethernet,
        WifiInfo? wifi = null,
        string? ip = "192.168.1.20",
        string? gateway = "192.168.1.1",
        IReadOnlyList<string>? dns = null,
        string? adapterId = "adapter-1") =>
        new(true, type, "Adapter", wifi, ip, gateway, dns ?? ["1.1.1.1", "8.8.8.8"], adapterId);

    private static Diagnosis Diag(DiagnosisOutcome outcome, params RemedyKind[] remedies) =>
        new(outcome, "Headline", "Advice", remedies);

    private static TroubleshootReport Report(Diagnosis diagnosis) =>
        new(new Dictionary<TroubleshootStep, StepState>(), diagnosis);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Constructor_WithSavedSpeedTest_ShowsItWithoutRunning()
    {
        _settings.Current.Network.LastSpeedTest = new SpeedTestResult(20, 80, 30, Measured);

        var vm = Create();

        Assert.True(vm.HasSpeedResult);
        Assert.Equal("80 Mbps", vm.DownloadText);
        Assert.Equal("30 Mbps", vm.UploadText);
        Assert.Equal("20 ms", vm.LatencyText);
        Assert.Equal(0, _speedTest.CallCount);
    }

    [Fact]
    public void Constructor_WithoutSavedSpeedTest_HasNoResult()
    {
        var vm = Create();

        Assert.False(vm.HasSpeedResult);
        Assert.Equal("-", vm.DownloadText);
    }

    [Fact]
    public void Constructor_ListsEveryTroubleshootStepAsPending()
    {
        var vm = Create();

        Assert.Equal(Enum.GetValues<TroubleshootStep>(), vm.Steps.Select(s => s.Step));
        Assert.All(vm.Steps, s => Assert.Equal(StepState.Pending, s.State));
    }

    [Fact]
    public void PageMetadata_IdentifiesInternetPage()
    {
        var vm = Create();

        Assert.Equal("Internet", vm.Title);
        Assert.Equal(1, vm.Order);
        Assert.Contains("Close anyway?", vm.BusyMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ConnectionType.WiFi, "Wi-Fi", true)]
    [InlineData(ConnectionType.Ethernet, "Network cable", false)]
    [InlineData(ConnectionType.Other, "Other", false)]
    [InlineData(ConnectionType.None, "None", false)]
    public async Task OnNavigatedTo_MapsConnectionTypeToTextAndWifiVisibility(ConnectionType type, string text, bool showWifi)
    {
        _status.Status = Status(type);
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.IsConnected);
        Assert.Equal(text, vm.ConnectionTypeText);
        Assert.Equal(showWifi, vm.ShowWifi);
    }

    [Fact]
    public async Task OnNavigatedTo_ShowsAddressesAndJoinsDnsServers()
    {
        _status.Status = Status();
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("192.168.1.20", vm.LocalIp);
        Assert.Equal("192.168.1.1", vm.Gateway);
        Assert.Equal("1.1.1.1, 8.8.8.8", vm.DnsServers);
    }

    [Fact]
    public async Task OnNavigatedTo_MissingAddresses_ShowDashes()
    {
        _status.Status = Status(ip: null, gateway: null, dns: []);
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("-", vm.LocalIp);
        Assert.Equal("-", vm.Gateway);
        Assert.Equal("-", vm.DnsServers);
    }

    [Fact]
    public async Task OnNavigatedTo_WifiWithName_ShowsNameAndSignal()
    {
        _status.Status = Status(ConnectionType.WiFi, new WifiInfo("HomeNet", 80));
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("HomeNet", vm.WifiName);
        Assert.Equal(4, vm.SignalBars);
        Assert.Equal("Excellent", vm.SignalLabel);
    }

    [Fact]
    public async Task OnNavigatedTo_WifiWithHiddenName_SaysWindowsHidesIt()
    {
        _status.Status = Status(ConnectionType.WiFi, new WifiInfo(null, 60));
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Name hidden by Windows", vm.WifiName);
    }

    [Fact]
    public async Task OnNavigatedTo_NoWifiInfo_ClearsSignal()
    {
        _status.Status = Status(ConnectionType.WiFi, new WifiInfo("HomeNet", 80));
        var vm = Create();
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        _status.Status = Status(ConnectionType.Ethernet);
        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Not available", vm.WifiName);
        Assert.Equal(0, vm.SignalBars);
        Assert.Equal(string.Empty, vm.SignalLabel);
    }

    [Theory]
    [InlineData(0, false, false, false, false)]
    [InlineData(10, true, false, false, false)]
    [InlineData(30, true, true, false, false)]
    [InlineData(60, true, true, true, false)]
    [InlineData(90, true, true, true, true)]
    public async Task SignalBars_DriveWhichBarsAreLit(int percent, bool b1, bool b2, bool b3, bool b4)
    {
        _status.Status = Status(ConnectionType.WiFi, new WifiInfo("Net", percent));
        var vm = Create();

        await vm.OnNavigatedToAsync(TestContext.Current.CancellationToken);

        Assert.Equal([b1, b2, b3, b4], [vm.HasBar1, vm.HasBar2, vm.HasBar3, vm.HasBar4]);
    }

    [Fact]
    public async Task RunTroubleshooter_ShowsDiagnosisAndSuggestedRemedies()
    {
        _elevation.IsElevated = true;
        _troubleshooter.Report = Report(Diag(DiagnosisOutcome.DnsProblem, RemedyKind.FlushDns, RemedyKind.RenewIp));
        var vm = Create();

        await vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.True(vm.HasDiagnosis);
        Assert.False(vm.DiagnosisIsHealthy);
        Assert.Equal("Headline", vm.DiagnosisHeadline);
        Assert.Equal("Advice", vm.DiagnosisAdvice);
        Assert.Equal([RemedyKind.FlushDns, RemedyKind.RenewIp], vm.SuggestedRemedies.Select(r => r.Kind));
        Assert.False(vm.ShowAdminBanner);
        Assert.False(vm.IsTroubleshooting);
    }

    [Fact]
    public async Task RunTroubleshooter_HealthyOutcome_MarksDiagnosisHealthy()
    {
        _troubleshooter.Report = Report(Diag(DiagnosisOutcome.Healthy));
        var vm = Create();

        await vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.True(vm.DiagnosisIsHealthy);
        Assert.Empty(vm.SuggestedRemedies);
    }

    [Theory]
    [InlineData(false, RemedyKind.ResetAdapter, true)]
    [InlineData(true, RemedyKind.ResetAdapter, false)]
    [InlineData(false, RemedyKind.FlushDns, false)]
    public async Task RunTroubleshooter_AdminBanner_OnlyWhenResetAdapterSuggestedWithoutElevation(
        bool elevated, RemedyKind suggested, bool expectBanner)
    {
        _elevation.IsElevated = elevated;
        _troubleshooter.Report = Report(Diag(DiagnosisOutcome.AdapterDown, suggested));
        var vm = Create();

        await vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.Equal(expectBanner, vm.ShowAdminBanner);
    }

    [Fact]
    public async Task RunTroubleshooter_RefreshesStatusAfterwards()
    {
        _status.Status = Status();
        _troubleshooter.Report = Report(Diag(DiagnosisOutcome.Healthy));
        var vm = Create();

        await vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.Equal("192.168.1.20", vm.LocalIp);
    }

    [Fact]
    public async Task RunTroubleshooter_ClearsPreviousDiagnosisPendingRemedyAndStepStates()
    {
        _troubleshooter.Report = Report(Diag(DiagnosisOutcome.Healthy));
        var vm = Create();
        vm.Steps[0].State = StepState.Failed;
        vm.RemedyMessage = "old";
        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.RenewIp));
        var gate = new TaskCompletionSource();
        _troubleshooter.Gate = gate;

        var run = vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.False(vm.HasDiagnosis);
        Assert.Null(vm.PendingRemedy);
        Assert.Equal(string.Empty, vm.RemedyMessage);
        Assert.Equal(StepState.Pending, vm.Steps[0].State);
        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task RunTroubleshooter_WhileRunning_IsBusyAndCannotRunAgain()
    {
        var gate = new TaskCompletionSource();
        _troubleshooter.Gate = gate;
        var vm = Create();

        var run = vm.RunTroubleshooterCommand.ExecuteAsync(null);

        Assert.True(vm.IsTroubleshooting);
        Assert.True(vm.IsBusyWithWork);
        Assert.False(vm.RunTroubleshooterCommand.CanExecute(null));
        gate.SetResult();
        await run;
        Assert.False(vm.IsBusyWithWork);
        Assert.True(vm.RunTroubleshooterCommand.CanExecute(null));
    }

    [Fact]
    public async Task RunTroubleshooter_WhenDisposedMidRun_StopsQuietly()
    {
        _troubleshooter.Gate = new TaskCompletionSource();
        var vm = Create();
        var run = vm.RunTroubleshooterCommand.ExecuteAsync(null);

        vm.Dispose();
        await run;

        Assert.False(vm.IsTroubleshooting);
        Assert.False(vm.HasDiagnosis);
    }

    [Fact]
    public async Task RequestRemedy_RenewIp_AsksForConfirmationWithoutRunningIt()
    {
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.RenewIp));

        Assert.Equal(RemedyKind.RenewIp, vm.PendingRemedy?.Kind);
        Assert.True(vm.HasPendingRemedy);
        Assert.Contains("drop for a few seconds", vm.PendingRemedyWarning, StringComparison.Ordinal);
        Assert.Equal(0, _remedies.RenewCalls);
    }

    [Fact]
    public async Task RequestRemedy_FlushDns_RunsImmediatelyAndShowsResultMessage()
    {
        _remedies.Result = new RemedyResult(RemedyOutcome.Done, "Cleared.");
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));

        Assert.Equal(1, _remedies.FlushCalls);
        Assert.False(vm.HasPendingRemedy);
        Assert.Equal("Cleared.", vm.RemedyMessage);
        Assert.False(vm.ShowAdminBanner);
        Assert.False(vm.IsRemedyRunning);
    }

    [Fact]
    public async Task RequestRemedy_OpenNetworkSettings_LaunchesWindowsSettings()
    {
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.OpenNetworkSettings));

        Assert.Equal(["ms-settings:network-status"], _urlLauncher.OpenedUrls);
    }

    [Fact]
    public async Task RequestRemedy_ResetAdapterWithoutElevation_ShowsBannerInsteadOfConfirming()
    {
        _elevation.IsElevated = false;
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.ResetAdapter));

        Assert.True(vm.ShowAdminBanner);
        Assert.Null(vm.PendingRemedy);
        Assert.Contains("administrator", vm.RemedyMessage, StringComparison.Ordinal);
        Assert.Equal(0, _remedies.ResetCalls);
    }

    [Fact]
    public async Task RequestRemedy_ResetAdapterElevated_AsksForConfirmation()
    {
        _elevation.IsElevated = true;
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.ResetAdapter));

        Assert.Equal(RemedyKind.ResetAdapter, vm.PendingRemedy?.Kind);
        Assert.Contains("switched off and on", vm.PendingRemedyWarning, StringComparison.Ordinal);
        Assert.Equal(0, _remedies.ResetCalls);
    }

    [Fact]
    public async Task RequestRemedy_Null_DoesNothing()
    {
        var vm = Create();
        vm.RemedyMessage = "keep";

        await vm.RequestRemedyCommand.ExecuteAsync(null);

        Assert.Equal("keep", vm.RemedyMessage);
        Assert.False(vm.HasPendingRemedy);
    }

    [Fact]
    public async Task RequestRemedy_WhileAnotherRemedyRuns_IsIgnored()
    {
        var gate = new TaskCompletionSource();
        _remedies.Gate = gate;
        var vm = Create();
        var first = vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));
        Assert.True(vm.IsRemedyRunning);

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));

        gate.SetResult();
        await first;
        Assert.Equal(1, _remedies.FlushCalls);
    }

    [Fact]
    public async Task RemedyRunning_DisablesTroubleshooterAndCountsAsBusy()
    {
        var gate = new TaskCompletionSource();
        _remedies.Gate = gate;
        var vm = Create();

        var run = vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));

        Assert.True(vm.IsBusyWithWork);
        Assert.False(vm.RunTroubleshooterCommand.CanExecute(null));
        gate.SetResult();
        await run;
        Assert.True(vm.RunTroubleshooterCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmRemedy_RunsPendingRenewIpAndClearsPending()
    {
        _remedies.Result = new RemedyResult(RemedyOutcome.Done, "Renewed.");
        var vm = Create();
        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.RenewIp));

        await vm.ConfirmRemedyCommand.ExecuteAsync(null);

        Assert.Equal(1, _remedies.RenewCalls);
        Assert.Null(vm.PendingRemedy);
        Assert.False(vm.HasPendingRemedy);
        Assert.Equal("Renewed.", vm.RemedyMessage);
    }

    [Fact]
    public async Task ConfirmRemedy_ResetAdapter_PassesCurrentAdapterId()
    {
        _elevation.IsElevated = true;
        _status.Status = Status(adapterId: "eth-42");
        var vm = Create();
        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.ResetAdapter));

        await vm.ConfirmRemedyCommand.ExecuteAsync(null);

        Assert.Equal("eth-42", _remedies.LastAdapterId);
    }

    [Fact]
    public async Task ConfirmRemedy_ResetAdapterWithoutAdapterId_PassesEmptyString()
    {
        _elevation.IsElevated = true;
        _status.Status = Status(adapterId: null);
        var vm = Create();
        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.ResetAdapter));

        await vm.ConfirmRemedyCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, _remedies.LastAdapterId);
    }

    [Fact]
    public async Task ConfirmRemedy_WithNothingPending_DoesNothing()
    {
        var vm = Create();

        await vm.ConfirmRemedyCommand.ExecuteAsync(null);

        Assert.Equal(0, _remedies.RenewCalls + _remedies.FlushCalls + _remedies.ResetCalls);
    }

    [Fact]
    public async Task CancelRemedy_DropsPendingRemedyWithoutRunningIt()
    {
        var vm = Create();
        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.RenewIp));

        vm.CancelRemedyCommand.Execute(null);

        Assert.Null(vm.PendingRemedy);
        Assert.False(vm.HasPendingRemedy);
        Assert.Equal(0, _remedies.RenewCalls);
    }

    [Theory]
    [InlineData(RemedyOutcome.NeedsAdmin, true)]
    [InlineData(RemedyOutcome.Done, false)]
    [InlineData(RemedyOutcome.Failed, false)]
    public async Task RemedyResult_NeedsAdminShowsBanner(RemedyOutcome outcome, bool expectBanner)
    {
        _remedies.Result = new RemedyResult(outcome, "msg");
        var vm = Create();

        await vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));

        Assert.Equal(expectBanner, vm.ShowAdminBanner);
        Assert.Equal("msg", vm.RemedyMessage);
    }

    [Fact]
    public async Task Remedy_WhenDisposedMidRun_ShowsStopped()
    {
        _remedies.Gate = new TaskCompletionSource();
        var vm = Create();
        var run = vm.RequestRemedyCommand.ExecuteAsync(new RemedyViewModel(RemedyKind.FlushDns));

        vm.Dispose();
        await run;

        Assert.Equal("Stopped.", vm.RemedyMessage);
        Assert.False(vm.IsRemedyRunning);
    }

    [Fact]
    public async Task StartSpeedTest_Success_ShowsResultAndSavesIt()
    {
        var result = new SpeedTestResult(25, 75, 20, Measured);
        _speedTest.Result = result;
        var vm = Create();

        await vm.StartSpeedTestCommand.ExecuteAsync(null);

        Assert.True(vm.HasSpeedResult);
        Assert.Equal("75 Mbps", vm.DownloadText);
        Assert.Equal("20 Mbps", vm.UploadText);
        Assert.Equal("25 ms", vm.LatencyText);
        Assert.Equal("Excellent", vm.VerdictHeadline);
        Assert.StartsWith("Tested ", vm.LastTestedText, StringComparison.Ordinal);
        Assert.Same(result, _settings.Current.Network.LastSpeedTest);
        Assert.Equal(string.Empty, vm.SpeedMessage);
        Assert.False(vm.IsSpeedTesting);
        Assert.Equal(string.Empty, vm.SpeedProgressText);
    }

    [Fact]
    public async Task StartSpeedTest_NoResult_ShowsServiceUnreachableMessageAndKeepsOldResult()
    {
        var old = new SpeedTestResult(10, 40, 10, Measured);
        _settings.Current.Network.LastSpeedTest = old;
        _speedTest.Result = null;
        var vm = Create();

        await vm.StartSpeedTestCommand.ExecuteAsync(null);

        Assert.Contains("couldn't reach", vm.SpeedMessage, StringComparison.Ordinal);
        Assert.Same(old, _settings.Current.Network.LastSpeedTest);
        Assert.Equal(0, _settings.UpdateCallCount);
        Assert.Equal("40 Mbps", vm.DownloadText);
    }

    [Fact]
    public async Task StartSpeedTest_MissingMeasurements_ShowDashes()
    {
        _speedTest.Result = new SpeedTestResult(null, 60, null, Measured);
        var vm = Create();

        await vm.StartSpeedTestCommand.ExecuteAsync(null);

        Assert.Equal("-", vm.LatencyText);
        Assert.Equal("-", vm.UploadText);
    }

    [Fact]
    public void ShowSpeedResult_PoorAndUnknownShareAWarningGlyphDifferentFromGoodOnes()
    {
        string GlyphFor(double? download)
        {
            _settings.Current.Network.LastSpeedTest = new SpeedTestResult(10, download, download, Measured);
            return Create().VerdictGlyph;
        }

        var poor = GlyphFor(1);
        var unknown = GlyphFor(null);
        var excellent = GlyphFor(100);

        Assert.Equal(poor, unknown);
        Assert.NotEqual(poor, excellent);
    }

    [Fact]
    public async Task StartSpeedTest_WhileRunning_IsBusyAndCannotStartAgain()
    {
        var gate = new TaskCompletionSource();
        _speedTest.Gate = gate;
        var vm = Create();

        var run = vm.StartSpeedTestCommand.ExecuteAsync(null);

        Assert.True(vm.IsSpeedTesting);
        Assert.True(vm.IsBusyWithWork);
        Assert.False(vm.StartSpeedTestCommand.CanExecute(null));
        gate.SetResult();
        await run;
        Assert.True(vm.StartSpeedTestCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelSpeedTest_StopsTestAndSaysSo()
    {
        _speedTest.Gate = new TaskCompletionSource();
        var vm = Create();
        var run = vm.StartSpeedTestCommand.ExecuteAsync(null);

        vm.CancelSpeedTestCommand.Execute(null);
        await run;

        Assert.Equal("Speed test stopped.", vm.SpeedMessage);
        Assert.False(vm.IsSpeedTesting);
        Assert.Equal(0, _settings.UpdateCallCount);
    }

    [Fact]
    public void CancelSpeedTest_WhenNoneRunning_DoesNothing()
    {
        var vm = Create();

        vm.CancelSpeedTestCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SpeedMessage);
    }

    [Fact]
    public async Task SpeedTest_WhenDisposedMidRun_StopsIt()
    {
        _speedTest.Gate = new TaskCompletionSource();
        var vm = Create();
        var run = vm.StartSpeedTestCommand.ExecuteAsync(null);

        vm.Dispose();
        await run;

        Assert.Equal("Speed test stopped.", vm.SpeedMessage);
    }

    [Fact]
    public async Task SetPageVisible_True_ListsAppsUsingTheInternet()
    {
        _appUsage.Usage = [new NetworkAppUsage("Chrome", 3), new NetworkAppUsage("Teams", 1)];
        var vm = Create();

        vm.SetPageVisible(true);
        await WaitUntilAsync(() => vm.HasApps);
        vm.SetPageVisible(false);

        Assert.Equal(["Chrome", "Teams"], vm.Apps.Select(a => a.Name));
        Assert.Equal("3 connections", vm.Apps[0].CountText);
        Assert.Equal("1 connection", vm.Apps[1].CountText);
    }

    [Fact]
    public async Task SetPageVisible_UsageReaderThrows_KeepsPageAliveWithNoApps()
    {
        _appUsage.Exception = new InvalidOperationException("boom");
        var vm = Create();

        vm.SetPageVisible(true);
        await WaitUntilAsync(() => _appUsage.CallCount >= 1);
        vm.SetPageVisible(false);

        Assert.False(vm.HasApps);
        Assert.Empty(vm.Apps);
    }

    [Fact]
    public async Task SetPageVisible_CalledTwiceWhileVisible_StartsOnlyOneLoop()
    {
        var vm = Create();

        vm.SetPageVisible(true);
        vm.SetPageVisible(true);
        await WaitUntilAsync(() => _appUsage.CallCount >= 1);
        vm.SetPageVisible(false);

        Assert.Equal(1, _appUsage.CallCount);
    }

    [Fact]
    public async Task SetPageVisible_AfterDispose_DoesNotStartRefreshing()
    {
        var vm = Create();
        vm.Dispose();

        vm.SetPageVisible(true);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, _appUsage.CallCount);
    }

    [Fact]
    public void Dispose_CalledTwice_IsSafe()
    {
        var vm = Create();

        vm.Dispose();
        vm.Dispose();
    }

    private sealed class FakeStatusProvider : INetworkStatusProvider
    {
        public NetworkStatus Status { get; set; } = NetworkStatus.Disconnected;

        public NetworkStatus GetStatus() => Status;
    }

    private sealed class FakeTroubleshooter : INetworkTroubleshooter
    {
        public TroubleshootReport Report { get; set; } = new(
            new Dictionary<TroubleshootStep, StepState>(),
            new Diagnosis(DiagnosisOutcome.Healthy, string.Empty, string.Empty, []));

        public TaskCompletionSource? Gate { get; set; }

        public async Task<TroubleshootReport> RunAsync(IProgress<TroubleshootStepUpdate>? progress, CancellationToken cancellationToken)
        {
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Report;
        }
    }

    private sealed class FakeRemedyService : INetworkRemedyService
    {
        public RemedyResult Result { get; set; } = new(RemedyOutcome.Done, "Done.");

        public TaskCompletionSource? Gate { get; set; }

        public int FlushCalls { get; private set; }

        public int RenewCalls { get; private set; }

        public int ResetCalls { get; private set; }

        public string? LastAdapterId { get; private set; }

        public Task<RemedyResult> FlushDnsAsync(CancellationToken cancellationToken)
        {
            FlushCalls++;
            return RunAsync(cancellationToken);
        }

        public Task<RemedyResult> RenewIpAsync(CancellationToken cancellationToken)
        {
            RenewCalls++;
            return RunAsync(cancellationToken);
        }

        public Task<RemedyResult> ResetAdapterAsync(string adapterId, CancellationToken cancellationToken)
        {
            ResetCalls++;
            LastAdapterId = adapterId;
            return RunAsync(cancellationToken);
        }

        private async Task<RemedyResult> RunAsync(CancellationToken cancellationToken)
        {
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Result;
        }
    }

    private sealed class FakeSpeedTestService : ISpeedTestService
    {
        public SpeedTestResult? Result { get; set; } = new(10, 10, 10, Measured);

        public TaskCompletionSource? Gate { get; set; }

        public int CallCount { get; private set; }

        public async Task<SpeedTestResult?> RunAsync(IProgress<SpeedTestPhase>? progress, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Gate is not null)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            return Result;
        }
    }

    private sealed class FakeAppUsageService : INetworkAppUsageService
    {
        private int _callCount;

        public IReadOnlyList<NetworkAppUsage> Usage { get; set; } = [];

        public Exception? Exception { get; set; }

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyList<NetworkAppUsage> GetUsage()
        {
            Interlocked.Increment(ref _callCount);
            return Exception is null ? Usage : throw Exception;
        }
    }
}
