using Microsoft.Extensions.Logging;
using PCManager.Core.Components;

namespace PCManager.App.Controls;

/// <inheritdoc cref="IComponentCardViewModelFactory"/>
public sealed class ComponentCardViewModelFactory : IComponentCardViewModelFactory
{
    private readonly IComponentService _componentService;
    private readonly ILoggerFactory _loggerFactory;

    public ComponentCardViewModelFactory(IComponentService componentService, ILoggerFactory loggerFactory)
    {
        _componentService = componentService;
        _loggerFactory = loggerFactory;
    }

    public ComponentCardViewModel Create(string componentId)
    {
        var definition = ComponentCatalog.Get(componentId);
        return new ComponentCardViewModel(
            definition, _componentService, _loggerFactory.CreateLogger<ComponentCardViewModel>());
    }
}
