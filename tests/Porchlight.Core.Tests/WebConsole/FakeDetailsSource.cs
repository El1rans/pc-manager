using Porchlight.Core.Safety;
using Porchlight.Core.Startup;
using Porchlight.Core.WebConsole;

namespace Porchlight.Core.Tests.WebConsole;

/// <summary>Returns fixed extra-view data and counts how often each was asked.</summary>
internal sealed class FakeDetailsSource : IWebConsoleDetailsSource
{
    public const string HostileName = "<script>alert(1)</script> & \"Co\"";

    public WebConsoleUpdates Updates { get; set; } = new(
        true,
        new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
        1,
        [new WebConsoleUpdateItem(HostileName, "1.0", "2.0")]);

    public WebConsoleStartup Startup { get; set; } = new(
        false,
        [new WebConsoleStartupItem(HostileName, "<b>Publisher</b>", true, StartupImpact.High)]);

    public WebConsoleSecurity Security { get; set; } = new(
        "This PC looks safe",
        SafetyLevel.Good,
        new WebConsoleSecurityCard("This PC is protected", SafetyLevel.Good, [HostileName + " - on"]),
        new WebConsoleSecurityCard("Windows is up to date", SafetyLevel.Good, []),
        new WebConsoleSecurityCard("1 remote-control program found", SafetyLevel.Attention, ["<img src=x onerror=1> - Running now"]));

    public int CallCount { get; private set; }

    public Task<WebConsoleUpdates> GetUpdatesAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Updates);
    }

    public Task<WebConsoleStartup> GetStartupAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Startup);
    }

    public Task<WebConsoleSecurity> GetSecurityAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Security);
    }
}
