using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

/// <summary>
/// Keeps Porchlight's own colours (amber accent, category hues - Themes/Palette.*.xaml) in step with
/// the Fluent Light/Dark theme. Fluent swaps its own dictionary when <see cref="Application.ThemeMode"/>
/// changes, but has no hook for app dictionaries, so the palette entries are copied straight into
/// <see cref="Application.Resources"/> (where they win over every merged dictionary, Fluent's included)
/// each time the effective theme changes. Fluent bakes the Windows accent colour into its control
/// brushes when it loads (buttons, check boxes, toggles, progress bars...), so on top of that every
/// Fluent brush painted in a Windows accent shade is re-created in the matching amber shade.
/// "System" follows Windows' app-mode setting live. In high
/// contrast the palette is removed entirely, so Windows' high-contrast colours are left alone.
/// See docs/specs/39-vivid-colour.md.
/// </summary>
public static class ThemePalette
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly Uri DarkUri = new("/Porchlight;component/Themes/Palette.Dark.xaml", UriKind.Relative);
    private static readonly Uri LightUri = new("/Porchlight;component/Themes/Palette.Light.xaml", UriKind.Relative);

    private static AppTheme _theme = AppTheme.System;
    private static bool _listening;
    private static List<object> _appliedKeys = [];

    /// <summary>Applies the palette for <paramref name="theme"/>. Must run on the UI thread.</summary>
    public static void Apply(Application app, AppTheme theme)
    {
        _theme = theme;
        if (!_listening)
        {
            _listening = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                // Windows reports a Light/Dark or high-contrast switch as General / Accessibility.
                if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Accessibility or UserPreferenceCategory.Color)
                {
                    // Background priority: after Fluent has swapped its own dictionary for the change.
                    app.Dispatcher.BeginInvoke(() => Refresh(app), System.Windows.Threading.DispatcherPriority.Background);
                }
            };
        }

        Refresh(app);
    }

    /// <summary>Whether <paramref name="theme"/> renders dark right now ("System" asks Windows).</summary>
    public static bool IsDark(AppTheme theme, bool? windowsAppsUseLightTheme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => windowsAppsUseLightTheme == false,
    };

    private static void Refresh(Application app)
    {
        foreach (var key in _appliedKeys)
        {
            app.Resources.Remove(key);
        }

        _appliedKeys = [];
        if (SystemParameters.HighContrast)
        {
            return;
        }

        var palette = new ResourceDictionary { Source = IsDark(_theme, ReadAppsUseLightTheme()) ? DarkUri : LightUri };
        foreach (var key in palette.Keys)
        {
            app.Resources[key] = palette[key];
            _appliedKeys.Add(key);
        }

        var accentMap = AccentMap(palette);
        foreach (var (key, brush) in FluentBrushes(app))
        {
            if (!palette.Contains(key)
                && accentMap.TryGetValue(Opaque(brush.Color), out var amber))
            {
                var replacement = new SolidColorBrush(Color.FromArgb(brush.Color.A, amber.R, amber.G, amber.B));
                replacement.Freeze();
                app.Resources[key] = replacement;
                _appliedKeys.Add(key);
            }
        }
    }

    /// <summary>Each Windows accent shade (as Fluent read it) -> the palette's amber shade of the same rank.</summary>
    private static Dictionary<Color, Color> AccentMap(ResourceDictionary palette)
    {
        var map = new Dictionary<Color, Color>();
        void Add(Color windows, string paletteKey)
        {
            if (palette[paletteKey] is Color amber)
            {
                map.TryAdd(Opaque(windows), amber);
            }
        }

        Add(SystemColors.AccentColor, "SystemAccentColor");
        Add(SystemColors.AccentColorLight1, "SystemAccentColorLight1");
        Add(SystemColors.AccentColorLight2, "SystemAccentColorLight2");
        Add(SystemColors.AccentColorLight3, "SystemAccentColorLight3");
        Add(SystemColors.AccentColorDark1, "SystemAccentColorDark1");
        Add(SystemColors.AccentColorDark2, "SystemAccentColorDark2");
        Add(SystemColors.AccentColorDark3, "SystemAccentColorDark3");
        return map;
    }

    private static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);

    /// <summary>Every solid brush in the Fluent theme dictionaries <see cref="Application.ThemeMode"/> added.</summary>
    private static IEnumerable<(object Key, SolidColorBrush Brush)> FluentBrushes(Application app)
    {
        var pending = new Stack<ResourceDictionary>(app.Resources.MergedDictionaries
            .Where(d => d.Source?.OriginalString.Contains("PresentationFramework.Fluent", StringComparison.OrdinalIgnoreCase) == true));
        while (pending.Count > 0)
        {
            var dictionary = pending.Pop();
            foreach (var merged in dictionary.MergedDictionaries)
            {
                pending.Push(merged);
            }

            foreach (var key in dictionary.Keys)
            {
                if (dictionary[key] is SolidColorBrush brush)
                {
                    yield return (key, brush);
                }
            }
        }
    }

    private static bool? ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value ? value != 0 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return null;
        }
    }
}
