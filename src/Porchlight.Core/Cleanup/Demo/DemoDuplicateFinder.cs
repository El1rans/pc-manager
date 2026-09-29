#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IDuplicateFinder"/> returning made-up groups - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoDuplicateFinder : IDuplicateFinder
{
    private static readonly DateTime Now = DateTime.UtcNow;

    public async Task<IReadOnlyList<DuplicateGroup>> FindAsync(
        DuplicateSearchOptions options, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);
        return
        [
            new DuplicateGroup(
                1_200L * 1024 * 1024,
                [
                    Make(@"C:\Users\Demo\Videos", "Holiday.mp4", 30),
                    Make(@"C:\Users\Demo\Desktop", "Holiday.mp4", 200),
                    Make(@"C:\Users\Demo\Documents\Backup", "Holiday copy.mp4", 400),
                ]),
            new DuplicateGroup(
                8L * 1024 * 1024,
                [
                    Make(@"C:\Users\Demo\Pictures", "Scan001.tif", 60),
                    Make(@"C:\Users\Demo\Downloads", "Scan001.tif", 90),
                ]),
        ];
    }

    private static DuplicateFile Make(string folder, string name, int daysOld) =>
        new(Path.Combine(folder, name), name, folder, Now.AddDays(-daysOld));
}
#endif
