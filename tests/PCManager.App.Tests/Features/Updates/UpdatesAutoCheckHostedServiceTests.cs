using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PCManager.App.Features.Updates;
using PCManager.Core.Settings;
using Xunit;

namespace PCManager.App.Tests.Features.Updates;

public sealed class UpdatesAutoCheckHostedServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;

    public UpdatesAutoCheckHostedServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PCManagerAppTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsStore = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// A fake <see cref="IServiceProvider"/> that throws if anything asks it to resolve a service -
    /// used to prove that when "Check for updates when PC Manager starts" is turned off,
    /// <see cref="UpdatesAutoCheckHostedService"/> never even attempts to resolve
    /// <c>UpdatesViewModel</c> (which would run a background <c>winget upgrade</c> listing).
    /// </summary>
    private sealed class ThrowingServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            throw new InvalidOperationException("Should not resolve any service when CheckOnStartup is disabled.");
    }

    [Fact]
    public async Task StartAsync_CheckOnStartupDisabled_DoesNotResolveAnyService()
    {
        _settingsStore.Update(s => s.Updates.CheckOnStartup = false);
        var service = new UpdatesAutoCheckHostedService(
            new ThrowingServiceProvider(), _settingsStore, NullLogger<UpdatesAutoCheckHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void CheckOnStartup_DefaultsToTrue()
    {
        Assert.True(_settingsStore.Current.Updates.CheckOnStartup);
    }
}
