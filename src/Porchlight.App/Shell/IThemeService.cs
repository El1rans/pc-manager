using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

/// <summary>Applies the chosen <see cref="AppTheme"/> to the whole running app.</summary>
public interface IThemeService
{
    /// <summary>Switches every open window (and any opened later) to <paramref name="theme"/>.</summary>
    void Apply(AppTheme theme);
}
