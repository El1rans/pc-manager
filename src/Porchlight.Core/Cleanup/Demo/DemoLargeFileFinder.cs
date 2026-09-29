#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="ILargeFileFinder"/> returning made-up files - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoLargeFileFinder : ILargeFileFinder
{
    private static readonly DateTime Now = DateTime.UtcNow;

    public Task<IReadOnlyList<FoundFile>> FindAsync(LargeFileSearchOptions options, CancellationToken cancellationToken)
    {
        IReadOnlyList<FoundFile> files = options.Extensions is null
            ?
            [
                Make(@"C:\Users\Demo\Videos", "Family holiday 2019.mp4", 3_400L * 1024 * 1024, 900),
                Make(@"C:\Users\Demo\Documents", "Old backup.zip", 1_900L * 1024 * 1024, 1400),
                Make(@"C:\Users\Demo\Desktop", "Wedding photos.iso", 900L * 1024 * 1024, 2000),
            ]
            :
            [
                Make(@"C:\Users\Demo\Downloads", "Setup-1.2.3.exe", 220L * 1024 * 1024, 200),
                Make(@"C:\Users\Demo\Downloads", "drivers.zip", 85L * 1024 * 1024, 400),
            ];
        return Task.FromResult(files);
    }

    private static FoundFile Make(string folder, string name, long bytes, int daysOld) =>
        new(Path.Combine(folder, name), name, folder, bytes, Now.AddDays(-daysOld));
}
#endif
