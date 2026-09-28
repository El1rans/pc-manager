using Microsoft.Extensions.Time.Testing;
using Porchlight.App.Shell;
using Xunit;

namespace Porchlight.App.Tests.Shell;

/// <summary>
/// Covers <see cref="CloakLifecycle"/>'s two-signals-race-to-uncloak-exactly-once logic in
/// isolation from WPF/DWM. A <see cref="FakeTimeProvider"/> makes the fallback timer's firing
/// deterministic (advanced explicitly), rather than depending on wall-clock timing.
/// </summary>
public sealed class CloakLifecycleTests
{
    private static readonly TimeSpan FallbackDelay = TimeSpan.FromMilliseconds(1500);

    [Fact]
    public void FallbackTimer_UncloaksWhenContentRenderedNeverFires()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        // ContentRendered never happens - e.g. an exception during the first layout/render pass.
        timeProvider.Advance(FallbackDelay);

        Assert.Equal(1, uncloakCount);
    }

    [Fact]
    public void FallbackTimer_DoesNotFireBeforeItsDelayElapses()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        timeProvider.Advance(FallbackDelay - TimeSpan.FromMilliseconds(1));

        Assert.Equal(0, uncloakCount);
    }

    [Fact]
    public void ContentRendered_UncloaksImmediatelyAndCancelsTheFallback()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        lifecycle.OnContentRendered();
        Assert.Equal(1, uncloakCount);

        // The fallback timer must have been cancelled by the real signal arriving first - advancing
        // well past its delay must not invoke the callback a second time.
        timeProvider.Advance(FallbackDelay * 2);

        Assert.Equal(1, uncloakCount);
    }

    [Fact]
    public void FallbackFiringFirst_MakesALaterContentRenderedANoOp()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        timeProvider.Advance(FallbackDelay);
        Assert.Equal(1, uncloakCount);

        lifecycle.OnContentRendered();

        Assert.Equal(1, uncloakCount);
    }

    [Fact]
    public void ForceUncloak_IsIdempotentWithContentRendered()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        lifecycle.ForceUncloak();
        lifecycle.OnContentRendered();
        lifecycle.ForceUncloak();

        Assert.Equal(1, uncloakCount);
    }

    [Fact]
    public void DoubleUncloak_InvokesTheCallbackOnlyOnce()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        using var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        lifecycle.OnContentRendered();
        lifecycle.OnContentRendered();
        lifecycle.ForceUncloak();

        Assert.Equal(1, uncloakCount);
    }

    [Fact]
    public void Dispose_StopsTheFallbackTimerWithoutUncloaking()
    {
        var timeProvider = new FakeTimeProvider();
        var uncloakCount = 0;
        var lifecycle = new CloakLifecycle(timeProvider, FallbackDelay, () => uncloakCount++);

        lifecycle.Dispose();
        timeProvider.Advance(FallbackDelay * 2);

        Assert.Equal(0, uncloakCount);
    }
}
