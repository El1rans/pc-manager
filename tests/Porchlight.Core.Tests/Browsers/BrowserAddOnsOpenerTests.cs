using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Browsers;
using Porchlight.Core.Processes;
using Xunit;

namespace Porchlight.Core.Tests.Browsers;

public sealed class BrowserAddOnsOpenerTests
{
    private sealed class FakeLocator(string? path) : IBrowserExecutableLocator
    {
        public string? Find(BrowserKind kind) => path;
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public List<(string File, string[] Args)> Started { get; } = [];

        public bool Throw { get; init; }

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IProgress<string>? onLine,
            IProgress<string>? onProgress,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void StartDetached(string fileName, IReadOnlyList<string> arguments)
        {
            if (Throw)
            {
                throw new System.ComponentModel.Win32Exception("nope");
            }

            Started.Add((fileName, [.. arguments]));
        }
    }

    [Theory]
    [InlineData(BrowserKind.Edge, "edge://extensions")]
    [InlineData(BrowserKind.Chrome, "chrome://extensions")]
    [InlineData(BrowserKind.Brave, "brave://extensions")]
    [InlineData(BrowserKind.Firefox, "about:addons")]
    public void TryOpen_StartsBrowserWithAddOnsUrl(BrowserKind kind, string url)
    {
        var runner = new FakeRunner();
        var opener = new BrowserAddOnsOpener(new FakeLocator(@"C:\b\browser.exe"), runner, NullLogger<BrowserAddOnsOpener>.Instance);

        Assert.True(opener.TryOpen(kind));

        var started = Assert.Single(runner.Started);
        Assert.Equal(@"C:\b\browser.exe", started.File);
        Assert.Equal([url], started.Args);
    }

    [Fact]
    public void TryOpen_BrowserNotFound_ReturnsFalse()
    {
        var runner = new FakeRunner();
        var opener = new BrowserAddOnsOpener(new FakeLocator(null), runner, NullLogger<BrowserAddOnsOpener>.Instance);

        Assert.False(opener.TryOpen(BrowserKind.Edge));
        Assert.Empty(runner.Started);
    }

    [Fact]
    public void TryOpen_StartFails_ReturnsFalse()
    {
        var opener = new BrowserAddOnsOpener(
            new FakeLocator(@"C:\b.exe"), new FakeRunner { Throw = true }, NullLogger<BrowserAddOnsOpener>.Instance);

        Assert.False(opener.TryOpen(BrowserKind.Chrome));
    }
}
