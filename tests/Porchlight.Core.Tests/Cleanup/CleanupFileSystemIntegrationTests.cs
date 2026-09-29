using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Cleanup;
using Porchlight.Core.Tests.Components;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>Runs the real <see cref="CleanupFileSystem"/> and <see cref="CleanupRunner"/> against a
/// temp directory this test creates itself - never against any real cleanup folder.</summary>
public sealed class CleanupFileSystemIntegrationTests : IDisposable
{
    private readonly string _sandbox =
        Path.Combine(Path.GetTempPath(), "PorchlightCleanupTests_" + Guid.NewGuid().ToString("N"));

    private string? _junction;

    public CleanupFileSystemIntegrationTests()
    {
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        // Remove the junction (only the link) before the recursive delete, so that can never
        // touch what it points at.
        if (_junction is not null && Directory.Exists(_junction))
        {
            Directory.Delete(_junction, recursive: false);
        }

        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    private static CleanupRunner CreateRunner() =>
        new(new CleanupFileSystem(), new FakeRecycleBin(), new FakeProcessProbe(), new FakeElevationService(),
            NullLogger<CleanupRunner>.Instance);

    private static CleanupCategory CategoryFor(string root) =>
        new(CleanupCategoryId.TemporaryFiles, "Temp", "x", false, true, [new CleanupRoot(root)], null, TimeSpan.FromHours(24));

    [Fact]
    public async Task Enumerate_IncludesHiddenFilesAndSizes()
    {
        var file = Path.Combine(_sandbox, "hidden.bin");
        await File.WriteAllBytesAsync(file, new byte[123], TestContext.Current.CancellationToken);
        File.SetAttributes(file, FileAttributes.Hidden);

        var entries = new CleanupFileSystem().EnumerateEntries(_sandbox);

        var entry = Assert.Single(entries);
        Assert.Equal(123, entry.Length);
        Assert.False(entry.IsDirectory);
    }

    [Fact]
    public async Task Runner_DeletesOldFilesAndEmptyFolders_ButKeepsRootAndYoungFiles()
    {
        var root = Path.Combine(_sandbox, "root");
        var nested = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(nested);
        var oldFile = Path.Combine(nested, "old.tmp");
        var youngFile = Path.Combine(root, "young.tmp");
        await File.WriteAllTextAsync(oldFile, "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(youngFile, "young", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-5));
        Directory.SetLastWriteTimeUtc(nested, DateTime.UtcNow.AddDays(-5));
        Directory.SetLastWriteTimeUtc(Path.Combine(root, "a"), DateTime.UtcNow.AddDays(-5));

        var result = await CreateRunner().CleanAsync([CategoryFor(root)], null, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(oldFile));
        Assert.False(Directory.Exists(Path.Combine(root, "a")));
        Assert.True(File.Exists(youngFile));
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public async Task Runner_NeverFollowsARealJunction()
    {
        var root = Path.Combine(_sandbox, "root");
        var outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var precious = Path.Combine(outside, "precious.txt");
        await File.WriteAllTextAsync(precious, "keep me", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(precious, DateTime.UtcNow.AddDays(-30));

        var junction = Path.Combine(root, "link");
        _junction = junction;
        if (!TryCreateJunction(junction, outside))
        {
            Assert.Skip("Could not create a directory junction in this environment.");
        }

        var result = await CreateRunner().CleanAsync([CategoryFor(root)], null, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(precious));
        Assert.True(Directory.Exists(junction));
        Assert.Equal(0, result.FilesDeleted);
    }

    [Fact]
    public void DeleteFile_RefusesAReparsePointFile()
    {
        var target = Path.Combine(_sandbox, "target.txt");
        File.WriteAllText(target, "x");
        var link = Path.Combine(_sandbox, "link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links need Developer Mode or elevation here.");
        }

        Assert.Throws<IOException>(() => new CleanupFileSystem().DeleteFile(link));
        Assert.True(File.Exists(target));
        File.Delete(link);
    }

    private static bool TryCreateJunction(string link, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(link);
        startInfo.ArgumentList.Add(target);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(link);
    }
}
