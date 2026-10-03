using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Features.RecentChanges;
using Porchlight.Core.Changes;
using Porchlight.Core.Startup;
using Porchlight.Core.WindowsServices;
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

    [Fact]
    public void ChangeJournalAndAutoRestorePointAreRegisteredOutsideDemoMode()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        App.ConfigureServices(builder.Services);
        using var provider = builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.IsType<ChangeJournal>(provider.GetRequiredService<IChangeJournal>());
        Assert.IsType<AutoRestorePoint>(provider.GetRequiredService<IAutoRestorePoint>());
        var undoTypes = provider.GetServices<IChangeUndoer>().Select(u => u.UndoType).ToList();
        Assert.Contains(StartupChangeUndoer.Type, undoTypes);
        Assert.Contains(ServiceStartTypeUndoer.Type, undoTypes);
        Assert.Contains(ServiceStateUndoer.Type, undoTypes);
        Assert.Equal(undoTypes.Count, undoTypes.Distinct(StringComparer.Ordinal).Count());
        Assert.NotNull(provider.GetRequiredService<RecentChangesViewModel>());
    }
}
