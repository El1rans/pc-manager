namespace Porchlight.Core.Health.Demo;

/// <summary>DEBUG demo data: a healthy SSD and a hard disk with a warning.</summary>
internal sealed class DemoDiskHealthService : IDiskHealthService
{
    private const long OneGb = 1024L * 1024 * 1024;

    public Task<HealthReadResult<DiskHealthSnapshot>> GetAsync(CancellationToken cancellationToken)
    {
        var ssd = new DiskHealthInfo("Demo NVMe SSD", 4, 953 * OneGb, 0, [2], 41, 6, 0);
        var hdd = new DiskHealthInfo("Demo Hard Disk", 3, 1863 * OneGb, 0, [2], 38, null, 12);
        DiskHealthReport ToReport(DiskHealthInfo i) => new(
            i.FriendlyName, DiskHealthEvaluator.DescribeMediaType(i.MediaType), i.SizeBytes,
            DiskHealthEvaluator.Evaluate(i), i.TemperatureCelsius, i.WearPercent);
        return Task.FromResult(HealthReadResult<DiskHealthSnapshot>.Ok(
            new DiskHealthSnapshot([ToReport(ssd), ToReport(hdd)], false)));
    }
}
