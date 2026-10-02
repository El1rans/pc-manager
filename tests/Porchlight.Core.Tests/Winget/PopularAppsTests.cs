using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class PopularAppsTests
{
    [Fact]
    public void All_IdsAreUniqueAndNeverStoreIds()
    {
        Assert.Equal(PopularApps.All.Count, PopularApps.All.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(PopularApps.All, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.Matches(@"^[A-Za-z0-9+.\-]+$", a.Id);
            Assert.DoesNotMatch(@"^9[A-Z0-9]{11}$", a.Id);
        });
    }
}
