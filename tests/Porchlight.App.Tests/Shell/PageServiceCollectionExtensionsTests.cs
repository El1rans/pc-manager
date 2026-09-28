using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Components;
using Porchlight.Core.RemoteSupport;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Shell;

public sealed class PageServiceCollectionExtensionsTests
{
    /// <summary>Documents WHY every page view model's Dispose must be idempotent: the container
    /// tracks the instance under both the concrete and the forwarding <see cref="IPage"/>
    /// registration, so it disposes it twice.</summary>
    [Fact]
    public async Task AddPage_ContainerDisposesPageViewModelTwiceOnShutdown()
    {
        var services = new ServiceCollection();
        services.AddPage<CountingPage, Border>();
        var provider = services.BuildServiceProvider();
        var page = provider.GetRequiredService<CountingPage>();
        Assert.Same(page, provider.GetServices<IPage>().Single());

        await provider.DisposeAsync();

        Assert.Equal(2, page.DisposeCount);
    }

    /// <summary>Regression for the "Error while shutting down the host" logged on every exit:
    /// disposing the container threw ObjectDisposedException from the "Get help" page's AnyDesk
    /// card on the second Dispose.</summary>
    [Fact]
    public async Task AddPage_RemoteSupportPage_ContainerShutdownDoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IComponentService, FakeComponentService>();
        services.AddSingleton<IComponentCardViewModelFactory, ComponentCardViewModelFactory>();
        services.AddSingleton<IAnyDeskService, FakeAnyDeskService>();
        services.AddSingleton<IClipboardService, FakeClipboardService>();
        services.AddSingleton<IUrlLauncher, FakeUrlLauncher>();
        services.AddSingleton<IWindowsVersionReader, FakeWindowsVersionReader>();
        services.AddSingleton<ISettingsStore, FakeSettingsStore>();
        services.AddPage<RemoteSupportViewModel, Border>();
        var provider = services.BuildServiceProvider();
        _ = provider.GetServices<IPage>().ToList();

        var exception = await Record.ExceptionAsync(() => provider.DisposeAsync().AsTask());

        Assert.Null(exception);
    }

    private sealed class CountingPage : PageViewModelBase, IDisposable
    {
        public int DisposeCount { get; private set; }

        public override string Title => "Counting";

        public override string Glyph => string.Empty;

        public override int Order => 0;

        public void Dispose() => DisposeCount++;
    }
}
