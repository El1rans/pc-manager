using Porchlight.Core.Checkup;

namespace Porchlight.App.Tests.Features.RemoteSupport;

/// <summary>Fake <see cref="ICheckupReportBuilder"/> returning a fixed small report.</summary>
internal sealed class FakeCheckupReportBuilder : ICheckupReportBuilder
{
    public int BuildCount { get; private set; }

    public Task<CheckupReport> BuildAsync(CancellationToken cancellationToken)
    {
        BuildCount++;
        return Task.FromResult(new CheckupReport(
            new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero),
            "TEST-PC",
            [new CheckupSectionResult("Drive space", CheckupSeverity.Ok, ["C: 100 GB free of 500 GB"])]));
    }
}
