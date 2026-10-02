using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Porchlight.App.Shell;

namespace Porchlight.App.Features.Startup;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
    }

    // Same app-wide command the AdminRequiredBanner uses.
    private void OnRestartAsAdminClick(object sender, RoutedEventArgs e) =>
        App.Services?.GetService<IShellService>()?.RestartElevatedCommand.Execute(null);
}
