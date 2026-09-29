using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Features.Updates;

namespace Porchlight.App.Features.Notifications;

/// <inheritdoc cref="IUpdateChecker"/>
public sealed class UpdateChecker(IServiceProvider serviceProvider) : IUpdateChecker
{
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return UpdateCheckResult.Skipped;
        }

        // The view model's collections are bound to the UI thread; resolve and drive it there (the
        // same reason UpdatesAutoCheckHostedService does).
        return await dispatcher.InvokeAsync(
            () => serviceProvider.GetRequiredService<UpdatesViewModel>().CheckForUpdatesAsync()).Task.Unwrap().WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
