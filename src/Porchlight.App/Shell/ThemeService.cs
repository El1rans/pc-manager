using System.Windows;
using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

/// <inheritdoc cref="IThemeService"/>
public sealed class ThemeService : IThemeService
{
    public void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var mode = ToThemeMode(theme);
        if (app.Dispatcher.CheckAccess())
        {
            app.ThemeMode = mode;
        }
        else
        {
            app.Dispatcher.BeginInvoke(() => app.ThemeMode = mode);
        }
    }

    /// <summary>Maps the stored choice to WPF's Fluent <see cref="ThemeMode"/>.</summary>
    internal static ThemeMode ToThemeMode(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeMode.Light,
        AppTheme.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };
}
