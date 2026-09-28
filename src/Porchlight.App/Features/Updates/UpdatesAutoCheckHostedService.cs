using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Updates;

/// <summary>
/// Runs the Updates page's first <c>winget upgrade</c> check at app startup, in the background,
/// regardless of whether the user ever navigates to the Updates page - see
/// <c>docs/specs/02-updates.md</c>: "Check runs automatically when the app starts... Nav badge
/// shows the count of non-ignored updates." <see cref="UpdatesViewModel.EnsureInitialCheckStartedAsync"/>
/// guards against also running this if the user opens the Updates page before this service does.
/// Only runs when <see cref="UpdatesSettings.CheckOnStartup"/> is enabled (the default) - see the
/// "Check for updates when Porchlight starts" toggle on the Updates page and
/// <c>docs/CODE_SIGNING_POLICY.md</c>'s privacy statement, which this toggle's existence keeps
/// accurate.
/// </summary>
public sealed class UpdatesAutoCheckHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<UpdatesAutoCheckHostedService> _logger;

    public UpdatesAutoCheckHostedService(
        IServiceProvider serviceProvider, ISettingsStore settingsStore, ILogger<UpdatesAutoCheckHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settingsStore.Current.Updates.CheckOnStartup)
        {
            _logger.LogInformation(
                "Skipping the startup update check - 'Check for updates when Porchlight starts' is turned off.");
            return Task.CompletedTask;
        }

        // Deliberately not awaited: a winget listing can take a few seconds, and every
        // IHostedService.StartAsync is awaited before the main window is shown (see App.OnStartup)
        // - this check must not delay that.
        _ = RunAsync();
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                // No WPF Application (e.g. a host built outside a running app); nothing to check.
                return;
            }

            // IHostedService.StartAsync runs on a thread-pool thread (see App.OnStartup, which
            // starts the host via Task.Run) - critically, that means resolving UpdatesViewModel
            // here directly (as a constructor dependency) would construct it on that thread the
            // first time this runs before it, which permanently binds its CollectionView to the
            // wrong thread and makes every later update throw. Resolving it lazily inside this
            // Dispatcher callback instead means its first construction - wherever that ends up
            // happening, here or via MainWindow's own resolution - is always on the UI thread.
            // Starting EnsureInitialCheckStartedAsync() here also means RefreshAsync's
            // ConfigureAwait(true) continuations resume on the Dispatcher, so its
            // ObservableCollection updates stay on the right thread throughout.
            await dispatcher.InvokeAsync(() => _serviceProvider.GetRequiredService<UpdatesViewModel>()
                    .EnsureInitialCheckStartedAsync())
                .Task.Unwrap()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // RefreshAsync already handles its own failures (WingetNotFoundException, etc.) by
            // reporting them on the page; this is a last-resort net for anything unexpected, since
            // an unobserved exception here would otherwise only surface as a crash-on-shutdown.
            _logger.LogError(ex, "Unexpected failure during the startup update check.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
