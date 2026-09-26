using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PCManager.App.Controls;

/// <summary>
/// Reusable card shown by feature pages when the component they depend on is missing or not
/// running: name, purpose, current status, and one primary action matching that status
/// ("Install" -> "Start" (if needed) -> "Ready", or "Retry" on error). Bind
/// <see cref="FrameworkElement.DataContext"/> to a <see cref="ComponentCardViewModel"/> obtained
/// from <see cref="IComponentCardViewModelFactory"/>.
/// </summary>
public partial class ComponentCard : UserControl
{
    public ComponentCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ComponentCardViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Loaded is an event handler, so this is the boundary: log and leave the card showing
            // whatever status it already had rather than crashing the page.
            var logger = App.Services?.GetService<ILogger<ComponentCard>>();
            logger?.LogError(ex, "ComponentCard failed to load its status.");
        }
    }
}
