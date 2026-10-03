using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Backup;
using Porchlight.Core.Checkup;
using Porchlight.Core.Checkup.Sections;
using Porchlight.Core.Health;
using Xunit;

namespace Porchlight.Core.Tests.Checkup;

public sealed class BackupCheckupSectionTests
{
    private sealed class FakeBackupStatus(HealthReadResult<BackupSnapshot> result) : IBackupStatusService
    {
        public Task<HealthReadResult<BackupSnapshot>> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private static Task<CheckupSectionResult?> Build(HealthReadResult<BackupSnapshot> result)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        return new BackupCheckupSection(new FakeBackupStatus(result), clock).BuildAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Nothing_IsProblem()
    {
        var section = await Build(HealthReadResult<BackupSnapshot>.Ok(
            new BackupSnapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, [])));

        Assert.Equal(CheckupSeverity.Problem, section!.Severity);
        Assert.Contains("Nothing is backing up your files.", section.Lines);
    }

    [Fact]
    public async Task Recent_IsOk()
    {
        var section = await Build(HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            new FileHistoryStatus(true, true, new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero)),
            OneDriveStatus.NotInstalled, [])));

        Assert.Equal(CheckupSeverity.Ok, section!.Severity);
    }

    [Fact]
    public async Task Stale_NeedsAttention()
    {
        var section = await Build(HealthReadResult<BackupSnapshot>.Ok(new BackupSnapshot(
            new FileHistoryStatus(true, true, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            OneDriveStatus.NotInstalled, [])));

        Assert.Equal(CheckupSeverity.NeedsAttention, section!.Severity);
    }

    [Fact]
    public async Task NeverNamesBackupPrograms()
    {
        var section = await Build(HealthReadResult<BackupSnapshot>.Ok(
            new BackupSnapshot(FileHistoryStatus.NotSetUp, OneDriveStatus.NotInstalled, ["SecretBackupTool"])));

        Assert.DoesNotContain(section!.Lines, line => line.Contains("SecretBackupTool", StringComparison.Ordinal));
        Assert.Contains("Another backup program is installed.", section.Lines);
    }

    [Fact]
    public async Task ReadFailure_IsNeedsAttention()
    {
        var section = await Build(HealthReadResult<BackupSnapshot>.Fail("nope"));

        Assert.Equal(CheckupSeverity.NeedsAttention, section!.Severity);
    }
}
