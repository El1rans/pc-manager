using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Porchlight.App.Controls;

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
        // A page that creates the card's view model asynchronously (or swaps it) may set
        // DataContext after Loaded has already fired and found nothing - reload whenever it
        // changes to a real view model instead of getting stuck on the default "not installed"
        // status forever.
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await TryLoadAsync().ConfigureAwait(true);

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        await TryLoadAsync().ConfigureAwait(true);

    private async Task TryLoadAsync()
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
            // Event handlers are the boundary here: log and leave the card showing whatever status
            // it already had rather than crashing the page.
            var logger = App.Services?.GetService<ILogger<ComponentCard>>();
            logger?.LogError(ex, "ComponentCard failed to load its status.");
        }
    }
}
