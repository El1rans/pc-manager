using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Porchlight.App.Features.Setup;

/// <inheritdoc cref="ISetupLauncher"/>
public sealed class SetupLauncher : ISetupLauncher
{
    private readonly IServiceProvider _serviceProvider;

    public SetupLauncher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void ShowSetup()
    {
        var window = _serviceProvider.GetRequiredService<SetupWindow>();
        if (Application.Current.MainWindow is { IsLoaded: true } mainWindow && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        window.ShowDialog();
    }
}
