using System.Windows;
using Microsoft.Extensions.Logging;

namespace PCManager.App.Features.Setup;

/// <summary>
/// First-run "Choose what to set up" dialog. Shown once automatically (when
/// <c>Setup.FirstRunCompleted</c> is false) and afterwards on demand from the sidebar footer's
/// "Set up optional features" button.
/// </summary>
public partial class SetupWindow : Window
{
    private readonly SetupViewModel _viewModel;
    private readonly ILogger<SetupWindow> _logger;

    public SetupWindow(SetupViewModel viewModel, ILogger<SetupWindow> logger)
    {
        _viewModel = viewModel;
        _logger = logger;
        DataContext = viewModel;
        InitializeComponent();

        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "First-run setup dialog failed to load component status.");
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.Dispose();
    }
}
