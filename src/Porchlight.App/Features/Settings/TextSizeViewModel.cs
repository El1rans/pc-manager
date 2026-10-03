using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;

namespace Porchlight.App.Features.Settings;

/// <summary>The "Text size" picker on Settings > General. Every change is written straight to
/// <see cref="ISettingsStore.Update"/> and applied live.</summary>
public sealed partial class TextSizeViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly ITextScaleService _textScale;

    [ObservableProperty]
    private TextSizeOption _selectedSize;

    public TextSizeViewModel(ISettingsStore settingsStore, ITextScaleService textScale)
    {
        _settingsStore = settingsStore;
        _textScale = textScale;

        // Assigned to the field: loading must not write settings back.
        _selectedSize = Options.FirstOrDefault(o => o.Value == settingsStore.Current.Appearance.TextSize) ?? Options[0];
    }

    public IReadOnlyList<TextSizeOption> Options { get; } =
    [
        new(TextSize.Normal, "Normal"),
        new(TextSize.Large, "Large"),
        new(TextSize.ExtraLarge, "Extra large"),
    ];

    partial void OnSelectedSizeChanged(TextSizeOption value)
    {
        _settingsStore.Update(s => s.Appearance.TextSize = value.Value);
        _textScale.Apply(value.Value);
    }
}
