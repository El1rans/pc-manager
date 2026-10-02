using Porchlight.App.Features.Health;
using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.App.Tests.Features.Health;

public sealed class BatteryCardViewModelTests
{
    private readonly FakeBatteryService _service = new();

    private BatteryCardViewModel CreateViewModel() => new(_service);

    private static BatteryReading Reading(
        long? design = 50_000, long? full = 45_000, int? cycles = null, int? charge = null,
        BatteryChargeState state = BatteryChargeState.Discharging) =>
        new(design, full, cycles, charge, state);

    [Fact]
    public async Task Refresh_NoBattery_StaysHidden()
    {
        _service.Result = HealthReadResult<BatteryReading?>.Ok(null);
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.False(viewModel.HasBattery);
        Assert.False(viewModel.IsVisible);
        Assert.False(viewModel.ShowError);
    }

    [Fact]
    public async Task Refresh_ReadFailing_IsShownRatherThanSilentlyHidden()
    {
        _service.Result = HealthReadResult<BatteryReading?>.Fail("WMI is unavailable.");
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.False(viewModel.HasBattery);
        Assert.Equal("Couldn't check. WMI is unavailable.", viewModel.ErrorText);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public async Task Refresh_AfterAFailure_ClearsTheError()
    {
        _service.Result = HealthReadResult<BatteryReading?>.Fail("WMI is unavailable.");
        var viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);

        _service.Result = HealthReadResult<BatteryReading?>.Ok(Reading());
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Null(viewModel.ErrorText);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public async Task Refresh_ShowsCheckingWhileReading()
    {
        var gate = new TaskCompletionSource<HealthReadResult<BatteryReading?>>();
        _service.Gate = gate;
        var viewModel = CreateViewModel();

        var refresh = viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.IsChecking);

        gate.SetResult(_service.Result);
        await refresh;
        Assert.False(viewModel.IsChecking);
    }

    [Theory]
    [InlineData(50_000L, 45_000L, HealthSeverity.Ok)]
    [InlineData(50_000L, 30_000L, HealthSeverity.Warning)]
    [InlineData(50_000L, 10_000L, HealthSeverity.Warning)]
    [InlineData(null, null, HealthSeverity.Neutral)]
    public async Task Refresh_MapsTheBatteryVerdictToASeverity(long? design, long? full, HealthSeverity severity)
    {
        _service.Result = HealthReadResult<BatteryReading?>.Ok(Reading(design, full));
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.True(viewModel.HasBattery);
        Assert.True(viewModel.IsVisible);
        Assert.Equal(severity, viewModel.Severity);
        Assert.NotEmpty(viewModel.VerdictText);
    }

    [Theory]
    [InlineData(
        50_000L, 45_000L, 120, 55, BatteryChargeState.Charging,
        "Holds 90% of its original charge - 55% charged now - Charging - 120 charge cycles")]
    [InlineData(null, null, null, null, BatteryChargeState.Discharging, "Running on battery")]
    public async Task Refresh_DetailText_ListsOnlyWhatTheBatteryReports(
        long? design, long? full, int? cycles, int? charge, BatteryChargeState state, string expected)
    {
        _service.Result = HealthReadResult<BatteryReading?>.Ok(Reading(design, full, cycles, charge, state));
        var viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(expected, viewModel.DetailText);
    }

    private sealed class FakeBatteryService : IBatteryService
    {
        public HealthReadResult<BatteryReading?> Result { get; set; } = HealthReadResult<BatteryReading?>.Ok(Reading());

        public TaskCompletionSource<HealthReadResult<BatteryReading?>>? Gate { get; set; }

        public Task<HealthReadResult<BatteryReading?>> GetAsync(CancellationToken cancellationToken) =>
            Gate?.Task ?? Task.FromResult(Result);
    }
}
