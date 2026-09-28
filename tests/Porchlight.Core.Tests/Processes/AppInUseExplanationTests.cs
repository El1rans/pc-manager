using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.Core.Tests.Processes;

public sealed class AppInUseExplanationTests
{
    [Fact]
    public void Build_NoLockingProcesses_ReturnsGenericExplanation()
    {
        var text = AppInUseExplanation.Build("OBS Studio", []);

        Assert.Equal(AppInUseExplanation.GenericExplanation, text);
    }

    [Fact]
    public void Build_OneLockingProcess_NamesItSingular()
    {
        // Real case: OBS Studio blocked by Chrome and another app holding its virtual-camera DLL
        // open while OBS itself was not running - see docs/specs/09-friendly-update-outcomes.md's
        // addendum.
        var text = AppInUseExplanation.Build("OBS Studio", ["Chrome"]);

        Assert.Equal("This program is using OBS Studio's files: Chrome. Close them, then try again.", text);
    }

    [Fact]
    public void Build_MultipleLockingProcesses_NamesThemAll()
    {
        var text = AppInUseExplanation.Build("OBS Studio", ["Chrome", "Claude"]);

        Assert.Equal(
            "These programs are using OBS Studio's files: Chrome, Claude. Close them, then try again.", text);
    }

    [Fact]
    public void Build_MoreThanMaxNamedProcesses_TruncatesWithCount()
    {
        var names = Enumerable.Range(1, AppInUseExplanation.MaxNamedProcesses + 3)
            .Select(i => $"App{i}")
            .ToList();

        var text = AppInUseExplanation.Build("OBS Studio", names);

        Assert.Contains("and 3 more", text);
        Assert.DoesNotContain("App" + (AppInUseExplanation.MaxNamedProcesses + 3), text.Replace("and 3 more", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_Dedupes_NotCallerResponsibility_StillReadableWithDuplicates()
    {
        // AppInUseExplanation.Build trusts its input; deduplication is IAppLockDetector's job
        // (RestartManagerLockDetector). This just documents that a caller passing duplicates still
        // gets valid (if repetitive) text rather than throwing.
        var text = AppInUseExplanation.Build("OBS Studio", ["Chrome", "Chrome"]);

        Assert.Contains("Chrome, Chrome", text);
    }
}
