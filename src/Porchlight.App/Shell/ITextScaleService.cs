using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

/// <summary>Applies the chosen <see cref="TextSize"/> to the page area of the running app.</summary>
public interface ITextScaleService
{
    /// <summary>Makes the page area (and any page opened later) use <paramref name="size"/>.</summary>
    void Apply(TextSize size);
}
