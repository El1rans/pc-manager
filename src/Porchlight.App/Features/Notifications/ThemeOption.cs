using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Notifications;

/// <summary>One choice in the "Theme" picker.</summary>
public sealed record ThemeOption(AppTheme Value, string Label);
