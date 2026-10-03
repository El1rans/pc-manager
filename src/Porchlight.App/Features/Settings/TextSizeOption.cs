using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>One choice in the "Text size" picker.</summary>
public sealed record TextSizeOption(TextSize Value, string Label);
