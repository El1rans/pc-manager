using Porchlight.Core.Cleanup;

namespace Porchlight.Core.Tests.Cleanup;

/// <summary>Points every known folder somewhere under a fake root (never the real disk). Individual
/// properties can be overridden to simulate a broken environment variable.</summary>
internal sealed class FakeCleanupPathProvider : ICleanupPathProvider
{
    public const string Base = @"C:\Fake";

    public string TempPath { get; set; } = Base + @"\Users\Test\AppData\Local\Temp";

    public string WindowsDirectory { get; set; } = Base + @"\Windows";

    public string LocalAppData { get; set; } = Base + @"\Users\Test\AppData\Local";

    public string ProgramData { get; set; } = Base + @"\ProgramData";

    public string UserProfile { get; set; } = Base + @"\Users\Test";

    public string ProgramFiles { get; set; } = Base + @"\Program Files";

    public string ProgramFilesX86 { get; set; } = Base + @"\Program Files (x86)";

    public string DownloadsFolder { get; set; } = Base + @"\Users\Test\Downloads";

    public IReadOnlyList<string> PersonalFolders { get; set; } = [];
}
