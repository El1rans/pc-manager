using System.Globalization;
using Porchlight.Core.Browsers;

namespace Porchlight.App.Features.Browsers;

/// <summary>Display model for one add-on row on the "Browser add-ons" page. Immutable.</summary>
public sealed class ExtensionItemViewModel
{
    private const string LooksFineGlyph = "";
    private const string ReviewGlyph = "";
    private const string WorthRemovingGlyph = "";

    public ExtensionItemViewModel(AssessedExtension assessed)
    {
        ArgumentNullException.ThrowIfNull(assessed);
        var extension = assessed.Extension;

        Name = extension.Name;
        Description = extension.Description;
        Level = assessed.Risk.Level;
        Flags = assessed.Risk.Flags;
        IsEnabled = extension.Enabled;

        var installed = extension.InstalledUtc is { } when_
            ? "Added " + when_.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            : "Added: date unknown";
        Details = $"Profile: {extension.ProfileName}  |  {(extension.Enabled ? "Turned on" : "Turned off")}  |  {installed}  |  {SourceText(extension.Source)}";

        (LevelText, LevelGlyph, Advice) = Level switch
        {
            ExtensionRiskLevel.WorthRemoving => (
                "Worth removing",
                WorthRemovingGlyph,
                "If you don't recognise this add-on, it is a good idea to remove it. See \"How to remove an add-on\" below."),
            ExtensionRiskLevel.Review => (
                "Review",
                ReviewGlyph,
                "Check that you know what this add-on is for. If you don't, consider removing it."),
            _ => ("Looks fine", LooksFineGlyph, string.Empty),
        };
    }

    public string Name { get; }

    public string Description { get; }

    public bool HasDescription => Description.Length > 0;

    public string Details { get; }

    public bool IsEnabled { get; }

    public ExtensionRiskLevel Level { get; }

    public string LevelText { get; }

    public string LevelGlyph { get; }

    public string Advice { get; }

    public bool HasAdvice => Advice.Length > 0;

    public IReadOnlyList<ExtensionRiskFlag> Flags { get; }

    public bool HasFlags => Flags.Count > 0;

    /// <summary>True for anything above "Looks fine".</summary>
    public bool NeedsReview => Level != ExtensionRiskLevel.LooksFine;

    private static string SourceText(ExtensionSource source) => source switch
    {
        ExtensionSource.Store => "From the official store",
        ExtensionSource.Sideloaded => "Added outside the store",
        ExtensionSource.Policy => "Installed by a policy",
        ExtensionSource.Developer => "Loaded in developer mode",
        _ => "Source unknown",
    };
}
