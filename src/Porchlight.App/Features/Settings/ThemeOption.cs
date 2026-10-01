using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>One choice in the "Theme" picker.</summary>
public sealed record ThemeOption(AppTheme Value, string Label);
