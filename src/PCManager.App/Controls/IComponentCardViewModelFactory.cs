using PCManager.Core.Components;

namespace PCManager.App.Controls;

/// <summary>
/// Creates a <see cref="ComponentCardViewModel"/> for a given component id, resolving its shared
/// dependencies from DI. Feature pages use this instead of taking a
/// <see cref="ComponentCardViewModel"/> dependency directly, since a page may need one card per
/// component and each card needs its own view model instance.
/// </summary>
public interface IComponentCardViewModelFactory
{
    ComponentCardViewModel Create(string componentId);
}
