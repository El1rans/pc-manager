using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Controls;
using Porchlight.App.Tests.Features.Setup;
using Porchlight.Core.Components;
using Xunit;

namespace Porchlight.App.Tests.Controls;

public sealed class ComponentCardViewModelTests
{
    [Fact]
    public void Dispose_Twice_DoesNotThrowAndUnsubscribes()
    {
        var componentService = new FakeComponentService();
        var factory = new ComponentCardViewModelFactory(componentService, NullLoggerFactory.Instance);
        var card = factory.Create(ComponentIds.AnyDesk);
        Assert.Equal(1, componentService.StatusChangedSubscriberCount);

        card.Dispose();
        var exception = Record.Exception(card.Dispose);

        Assert.Null(exception);
        Assert.Equal(0, componentService.StatusChangedSubscriberCount);
    }
}
