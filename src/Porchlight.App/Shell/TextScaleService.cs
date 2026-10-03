using System.Windows;
using System.Windows.Media;
using Porchlight.Core.Settings;

namespace Porchlight.App.Shell;

/// <summary>Publishes the page-area scale as an application resource that the main window's content
/// area binds its <c>LayoutTransform</c> to, so a change shows straight away. The nav rail, tray menu
/// and toasts are not scaled. See <c>docs/specs/37-larger-text.md</c>.</summary>
public sealed class TextScaleService : ITextScaleService
{
    /// <summary>Resource key of the <see cref="ScaleTransform"/> the content area uses.</summary>
    public const string TransformResourceKey = "PageTextScaleTransform";

    public void Apply(TextSize size)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            Apply(size, app.Resources);
        }
        else
        {
            app.Dispatcher.BeginInvoke(() => Apply(size, app.Resources));
        }
    }

    /// <summary>Writes the transform for <paramref name="size"/> into <paramref name="resources"/>.</summary>
    public static void Apply(TextSize size, ResourceDictionary resources)
    {
        var factor = TextSizeScale.FactorFor(size);
        var transform = new ScaleTransform(factor, factor);
        transform.Freeze();
        resources[TransformResourceKey] = transform;
    }
}
