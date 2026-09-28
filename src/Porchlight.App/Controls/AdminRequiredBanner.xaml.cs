using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;

namespace Porchlight.App.Controls;

/// <summary>
/// Banner shown by feature pages that need administrator rights to show everything. Pair with
/// each page's own check of <c>IElevationService.IsElevated</c> to control its visibility.
/// </summary>
public partial class AdminRequiredBanner : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(AdminRequiredBanner),
        new PropertyMetadata("This page needs administrator rights to show everything."));

    public static readonly DependencyProperty RestartCommandProperty = DependencyProperty.Register(
        nameof(RestartCommand), typeof(ICommand), typeof(AdminRequiredBanner));

    public AdminRequiredBanner()
    {
        InitializeComponent();

        // Default to the app-wide restart-elevated command so a page only needs to set
        // RestartCommand explicitly when it wants different behaviour.
        Loaded += OnLoaded;
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ICommand? RestartCommand
    {
        get => (ICommand?)GetValue(RestartCommandProperty);
        set => SetValue(RestartCommandProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (RestartCommand is null && App.Services?.GetService<IShellService>() is { } shellService)
        {
            RestartCommand = shellService.RestartElevatedCommand;
        }
    }
}
