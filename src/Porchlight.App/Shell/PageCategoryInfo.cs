using Porchlight.App.Controls;

namespace Porchlight.App.Shell;

/// <summary>Display data for one <see cref="PageCategory"/>.</summary>
/// <param name="Category">The category described.</param>
/// <param name="Title">Rail text.</param>
/// <param name="Glyph">Segoe Fluent Icons glyph.</param>
/// <param name="Order">Position among the unpinned categories, ascending.</param>
/// <param name="IsPinnedToBottom">True for a category shown in its own group at the bottom of the rail.</param>
/// <param name="Hue">Colour of the category's rail icon (docs/specs/39-vivid-colour.md).</param>
public sealed record PageCategoryInfo(PageCategory Category, string Title, string Glyph, int Order, bool IsPinnedToBottom, Hue Hue = Hue.Neutral);
