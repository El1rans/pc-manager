using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Porchlight.App.Tests.Shell;

/// <summary>Builds the app's real service container exactly as <see cref="App"/> does and lets the
/// container validate every registration. 0.2.0 shipped with <c>StartupService</c> missing two of
/// its dependencies outside demo mode, which only showed up as a startup crash on users' PCs.</summary>
public sealed class AppCompositionTests
{
    [Fact]
    public void EveryRegisteredServiceCanBeConstructed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        App.ConfigureServices(builder.Services);

        var exception = Record.Exception(() =>
        {
            using var provider = builder.Services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        });

        Assert.Null(exception);
    }
}
