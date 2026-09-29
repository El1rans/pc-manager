namespace Porchlight.Core.Health.Demo;

/// <summary>DEBUG demo data: a few made-up problems.</summary>
internal sealed class DemoProblemEventReader : IProblemEventReader
{
    public Task<HealthReadResult<IReadOnlyList<HealthEventRecord>>> ReadAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var list = new List<HealthEventRecord>();
        for (var i = 1; i <= 4; i++)
        {
            list.Add(new HealthEventRecord("Application Error", 1000, now.AddDays(-i * 3), ["chrome.exe"]));
        }

        list.Add(new HealthEventRecord("Microsoft-Windows-Kernel-Power", 41, now.AddDays(-5), []));
        list.Add(new HealthEventRecord("Microsoft-Windows-WindowsUpdateClient", 20, now.AddDays(-8), []));
        return Task.FromResult(HealthReadResult<IReadOnlyList<HealthEventRecord>>.Ok(list));
    }
}
