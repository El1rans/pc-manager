using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Features.Updates;

namespace Porchlight.App.Features.Notifications;

/// <inheritdoc cref="IUpdateChecker"/>
public sealed class UpdateChecker(IServiceProvider serviceProvider) : IUpdateChecker
{
    /// <summary>Set by <c>UpdatesViewModel.UpdateSummary</c> only after a successful listing
    /// ("3 updates available - last checked 09:00"); the failure paths set other text.</summary>
    private const string SuccessMarker = "last checked";

    public async Task<int?> CheckAsync(CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return null;
        }

        // The view model's collections are bound to the UI thread; resolve and drive it there (the
        // same reason UpdatesAutoCheckHostedService does).
        return await dispatcher.InvokeAsync(async () =>
        {
            var viewModel = serviceProvider.GetRequiredService<UpdatesViewModel>();
            if (viewModel.IsBusy || viewModel.IsUpdating)
            {
                return (int?)null;
            }

            await viewModel.RefreshAsync(quiet: false).ConfigureAwait(true);

            return viewModel.SummaryText.Contains(SuccessMarker, StringComparison.Ordinal)
                ? viewModel.Packages.Count(p => !p.IsHiddenByDefault)
                : (int?)null;
        }).Task.Unwrap().WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
