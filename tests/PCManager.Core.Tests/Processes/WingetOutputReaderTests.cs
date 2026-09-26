using PCManager.Core.Processes;
using Xunit;

namespace PCManager.Core.Tests.Processes;

public sealed class WingetOutputReaderTests
{
    [Fact]
    public void Feed_LfOnly_SplitsLines()
    {
        var reader = new WingetOutputReader();

        reader.Feed("line one\nline two\n");

        Assert.Equal(["line one", "line two"], reader.Lines);
    }

    [Fact]
    public void Feed_CrLf_SplitsLines_NotProgress()
    {
        var lines = new List<string>();
        var progress = new List<string>();
        var reader = new WingetOutputReader(new Progress(lines.Add), new Progress(progress.Add));

        reader.Feed("Found AnyDesk\r\nInstalling...\r\n");

        Assert.Equal(["Found AnyDesk", "Installing..."], reader.Lines);
        Assert.Equal(["Found AnyDesk", "Installing..."], lines);
        Assert.Empty(progress);
    }

    [Fact]
    public void Feed_LoneCarriageReturn_IsProgressNotLine()
    {
        var lines = new List<string>();
        var progress = new List<string>();
        var reader = new WingetOutputReader(new Progress(lines.Add), new Progress(progress.Add));

        // winget redraws a percentage in place: "\r  10%\r  20%\r  30%" with no trailing "\n".
        reader.Feed("  10%\r  20%\r  30%");
        reader.Complete();

        Assert.Equal(["10%", "20%"], progress);
        // The final, un-terminated "  30%" is flushed as a line by Complete() - there is no more
        // input that could turn its leading "\r" (already consumed) into a redraw. Unlike progress
        // reports, lines are not trimmed.
        Assert.Equal(["  30%"], lines);
    }

    [Fact]
    public void Feed_SpinnerFrames_AreDropped()
    {
        var progress = new List<string>();
        var reader = new WingetOutputReader(onProgress: new Progress(progress.Add));

        reader.Feed("\r-\r\\\r|\r/\r");
        reader.Complete();

        Assert.Empty(progress);
    }

    [Fact]
    public void Feed_CrLfSplitAcrossTwoFeedCalls_StillRecognisedAsOneLineEnding()
    {
        var lines = new List<string>();
        var reader = new WingetOutputReader(new Progress(lines.Add));

        // Simulate a buffer boundary landing exactly between \r and \n.
        reader.Feed("Downloading\r");
        reader.Feed("\nUnpacking\r\n");

        Assert.Equal(["Downloading", "Unpacking"], reader.Lines);
        Assert.Equal(["Downloading", "Unpacking"], lines);
    }

    [Fact]
    public void Feed_BlankLines_NotReportedAsLogButStillCounted()
    {
        var lines = new List<string>();
        var reader = new WingetOutputReader(new Progress(lines.Add));

        reader.Feed("first\n\nsecond\n");

        Assert.Equal(["first", "", "second"], reader.Lines);
        Assert.Equal(["first", "second"], lines);
    }

    [Fact]
    public void Complete_TrailingTextWithNoNewline_IsEmittedAsFinalLine()
    {
        var reader = new WingetOutputReader();

        reader.Feed("no trailing newline");
        reader.Complete();

        Assert.Equal(["no trailing newline"], reader.Lines);
    }

    [Fact]
    public void Complete_NothingBuffered_AddsNoExtraLine()
    {
        var reader = new WingetOutputReader();

        reader.Feed("one\n");
        reader.Complete();

        Assert.Equal(["one"], reader.Lines);
    }

    /// <summary>Minimal <see cref="IProgress{T}"/> that just invokes a delegate synchronously,
    /// so assertions can observe reports in call order without needing a SynchronizationContext.</summary>
    private sealed class Progress(Action<string> onReport) : IProgress<string>
    {
        public void Report(string value) => onReport(value);
    }
}
