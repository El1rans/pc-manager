namespace Porchlight.Core.Settings;

/// <summary>Settings for how the app looks. See <c>docs/specs/25-theme-setting.md</c>.</summary>
public sealed class AppearanceSettings
{
    /// <summary>"Match Windows / Light / Dark".</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>"Normal / Large / Extra large" text. See <c>docs/specs/37-larger-text.md</c>.</summary>
    public TextSize TextSize { get; set; } = TextSize.Normal;
}
