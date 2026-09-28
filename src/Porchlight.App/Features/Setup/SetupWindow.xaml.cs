using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using Porchlight.App.Shell;
using Porchlight.Core.Components;

namespace Porchlight.App.Features.Setup;

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

        // See WhiteFlashGuard: without this, the freshly shown window can paint solid white until
        // the user clicks it.
        WhiteFlashGuard.Attach(this);

        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
        Closing += OnClosing;
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

    /// <summary>
    /// An install is running to completion regardless of the dialog (see
    /// <see cref="IComponentService.InstallAsync"/> - once winget has launched, cancellation only
    /// stops us from waiting on it, not the install itself), so closing the window here - whether
    /// via Alt+F4, the X button, or anything else - while <see cref="SetupViewModel.IsBusy"/> would
    /// just hide the window while winget (and possibly the PawnIO driver installer) keeps running
    /// unattended. Refuse the close until it finishes.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            e.Cancel = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        // Closing via the window's own X button (rather than Skip or Close) still counts as
        // first-run having been shown - otherwise it would reappear every launch. Idempotent, so
        // it does not matter whether Skip/PrimaryAction already marked it.
        _viewModel.MarkFirstRunCompleted();
        _viewModel.Dispose();
    }
}
